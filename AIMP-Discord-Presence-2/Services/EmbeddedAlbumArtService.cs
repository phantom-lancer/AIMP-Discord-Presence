using AIMP.SDK.FileManager.Objects;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace AIMP_Discord_Presence_2.Services
{
	/// <summary>
	/// Takes the cover embedded into the track file itself (ID3APIC / FLAC PICTURE / MP4 covr),
	/// scales it down and uploads it to a plain, key-less HTTP host, because Discord Rich Presence
	/// is only able to display images that are available by an https URL.
	/// Nothing here ever blocks the caller: a cover that is not ready yet is simply not returned.
	/// </summary>
	public sealed class EmbeddedAlbumArtService : IPrefetchingAlbumArtService
	{
		public const string DEFAULT_UPLOAD_ENDPOINT = "https://litterbox.catbox.moe/resources/internals/api.php";
		public const string DEFAULT_UPLOAD_EXPIRY = "1h";
		public const string DEFAULT_USER_AGENT = "AIMP-Discord-Presence-2/0.0.3";

		private const int JPEG_QUALITY = 90;
		private const int HTTP_TIMEOUT_SECONDS = 15;
		private const int MAX_DISK_CACHE_ENTRIES = 500;

		/// <summary>How long a failed track is not retried, so an unreachable host is not hammered.</summary>
		private static readonly TimeSpan FAILURE_COOLDOWN = TimeSpan.FromMinutes(10);

		public sealed class CacheEntry
		{
			[JsonProperty("url")]
			public string url;

			[JsonProperty("expires")]
			public DateTime expires;
		}

		private readonly HttpClient _http;
		private readonly StreamWriter _log;
		private readonly string _logPath;
		private readonly string _cachePath;

		private readonly string _uploadEndpoint;
		private readonly string _uploadExpiry;
		private readonly TimeSpan _uploadLifetime;
		private readonly int _maxDimension;
		private readonly int _minDimension;
		private readonly int _retryCount;
		private readonly int _retryDelay;
		private readonly bool _cacheEnabled;

		private readonly IAlbumArtService _fallback;
		private readonly object _lock = "";
		private readonly Dictionary<string, CacheEntry> _cache = new Dictionary<string, CacheEntry>();
		private readonly HashSet<string> _inWork = new HashSet<string>();
		private readonly Dictionary<string, DateTime> _failures = new Dictionary<string, DateTime>();

		public EmbeddedAlbumArtService(string uploadEndpoint, string uploadExpiry, int maxDimension, int minDimension, bool useInternetFallback, string fallbackUserAgent, int retryCount, int retryDelay, bool cacheEnabled)
		{
			EnsureModernTls();

			_http = new HttpClient
			{
				Timeout = TimeSpan.FromSeconds(HTTP_TIMEOUT_SECONDS),
			};
			_http.DefaultRequestHeaders.Add("User-Agent", "AIMP-Discord-Presence-2");

			_uploadEndpoint = string.IsNullOrWhiteSpace(uploadEndpoint) ? DEFAULT_UPLOAD_ENDPOINT : uploadEndpoint.Trim();
			_uploadExpiry = string.IsNullOrWhiteSpace(uploadExpiry) ? DEFAULT_UPLOAD_EXPIRY : uploadExpiry.Trim();
			_uploadLifetime = ParseLifetime(_uploadExpiry);
			_maxDimension = maxDimension < 64 ? 512 : maxDimension;
			_minDimension = minDimension < 16 ? 16 : minDimension;
			_retryCount = retryCount < 1 ? 1 : retryCount;
			_retryDelay = retryDelay < 100 ? 500 : retryDelay;
			_cacheEnabled = cacheEnabled;

			var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BowieD_AIMPDiscordPresence2", "EmbeddedProvider");

			if (!Directory.Exists(dir))
			{
				Directory.CreateDirectory(dir);
			}

			_logPath = Path.Combine(dir, "uploads.log");
			_cachePath = Path.Combine(dir, "cache.json");

			_log = new StreamWriter(new FileStream(_logPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
			{
				AutoFlush = true,
			};

			// Covers are almost never embedded into mp3s, so an internet lookup may be used as a last resort.
			_fallback = useInternetFallback
				? new MusicBrainzAlbumArtService(string.IsNullOrWhiteSpace(fallbackUserAgent) ? DEFAULT_USER_AGENT : fallbackUserAgent.Trim())
				: null;

			if (_cacheEnabled)
			{
				LoadCache();
			}
		}

		/// <summary>
		/// AIMP starts with an outdated ServicePointManager default (SSL3/TLS1.0) and modern https hosts
		/// refuse to talk to it, which surfaces as a bare "an error occurred while sending the request".
		/// </summary>
		public static void EnsureModernTls()
		{
			try
			{
				if ((ServicePointManager.SecurityProtocol & SecurityProtocolType.Tls12) == 0)
				{
					ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
				}
			}
			catch
			{
				// nothing we can do about it, the upload will just fail and be logged
			}
		}

		private static TimeSpan ParseLifetime(string expiry)
		{
			var digits = new string((expiry ?? "").TakeWhile(char.IsDigit).ToArray());

			int hours;

			return int.TryParse(digits, out hours) && hours > 0
				? TimeSpan.FromHours(hours)
				: TimeSpan.FromHours(1);
		}

		private static string ComputeKey(IAimpFileInfo fileInfo)
		{
			string modified;

			try
			{
				modified = File.Exists(fileInfo.FileName)
					? File.GetLastWriteTimeUtc(fileInfo.FileName).Ticks.ToString()
					: "0";
			}
			catch
			{
				modified = "0";
			}

			using (var sha1 = SHA1.Create())
			{
				var bytes = Encoding.UTF8.GetBytes($"{fileInfo.FileName}|{fileInfo.FileSize}|{modified}");

				return string.Join("", sha1.ComputeHash(bytes).Select(d => d.ToString("x2")));
			}
		}

		private static bool HasUsableCover(IAimpFileInfo fileInfo, int minDimension)
		{
			var art = fileInfo?.AlbumArt;

			// Most mp3 files carry either nothing or a 1x1 placeholder image, treat both as "no cover".
			return art != null && art.Width >= minDimension && art.Height >= minDimension;
		}

		private bool IsCoolingDown(string key)
		{
			return _failures.TryGetValue(key, out var retryAt) && DateTime.UtcNow < retryAt;
		}

		public string TryGetImageUrl(IAimpFileInfo fileInfo)
		{
			if (fileInfo is null)
				return "";

			if (!HasUsableCover(fileInfo, _minDimension))
				return _fallback?.TryGetImageUrl(fileInfo) ?? "";

			var key = ComputeKey(fileInfo);
			string cachedUrl = "";

			lock (_lock)
			{
				if (IsCoolingDown(key))
					return _fallback?.TryGetImageUrl(fileInfo) ?? "";

				if (_cache.TryGetValue(key, out var entry))
				{
					cachedUrl = entry.url ?? "";

					// A cached url may have expired already, but it still beats no cover at all:
					// refresh it in the background and show what we already have meanwhile.
					if (DateTime.UtcNow < entry.expires)
						return cachedUrl;
				}

				if (!_inWork.Add(key))
					return cachedUrl;
			}

			StartUpload(key, fileInfo.AlbumArt);

			return cachedUrl;
		}

		public void Prefetch(IAimpFileInfo fileInfo)
		{
			if (fileInfo is null)
				return;

			if (!HasUsableCover(fileInfo, _minDimension))
				return;

			var key = ComputeKey(fileInfo);

			lock (_lock)
			{
				if (IsCoolingDown(key))
					return;

				if (_cache.TryGetValue(key, out var entry) && DateTime.UtcNow < entry.expires)
					return;

				if (!_inWork.Add(key))
					return;
			}

			StartUpload(key, fileInfo.AlbumArt);
		}

		private void StartUpload(string key, Image source)
		{
			Task.Run(async () => await UploadCoverAsync(key, source));
		}

		private async Task UploadCoverAsync(string key, Image source)
		{
			string url = "";

			for (int i = 0; i < _retryCount; i++)
			{
				try
				{
					var payload = EncodeJpeg(source);

					using (var multiPart = new MultipartFormDataContent
					{
						{ new StringContent("fileupload"), "reqtype" },
						{ new StringContent(_uploadExpiry), "time" },
						{ new ByteArrayContent(payload), "fileToUpload", "cover.jpg" },
					})
					{
						var response = await _http.PostAsync(_uploadEndpoint, multiPart);

						var body = await response.Content.ReadAsStringAsync();

						if (!response.IsSuccessStatusCode)
						{
							await LogAsync($"[attempt #{i + 1}] upload failed with {(int)response.StatusCode} {response.ReasonPhrase}: {body}");

							await Task.Delay(_retryDelay);
							continue;
						}

						url = ExtractUrl(body);

						if (string.IsNullOrWhiteSpace(url))
						{
							await LogAsync($"[attempt #{i + 1}] could not get an image url out of the response: {body}");

							await Task.Delay(_retryDelay);
							continue;
						}

						await LogAsync($"uploaded {url}");

						break;
					}
				}
				catch (Exception ex)
				{
					await LogAsync($"[attempt #{i + 1}] could not upload the cover: {ex.Message}");

					await Task.Delay(_retryDelay);
				}
			}

			lock (_lock)
			{
				_inWork.Remove(key);

				if (string.IsNullOrWhiteSpace(url))
				{
					// do not hammer a host that is unreachable, try again in a few minutes
					_failures[key] = DateTime.UtcNow.Add(FAILURE_COOLDOWN);

					return;
				}

				_failures.Remove(key);

				_cache[key] = new CacheEntry()
				{
					url = url,
					expires = DateTime.UtcNow.Add(_uploadLifetime).AddMinutes(-1),
				};

				while (_cache.Count > MAX_DISK_CACHE_ENTRIES)
				{
					_cache.Remove(_cache.Keys.First());
				}
			}

			SaveCache();
		}

		/// <summary>
		/// Scales the cover down (Discord does not need huge images) and encodes it as JPEG.
		/// </summary>
		private byte[] EncodeJpeg(Image source)
		{
			int width = source.Width;
			int height = source.Height;

			if (Math.Max(width, height) > _maxDimension)
			{
				var scale = (double)_maxDimension / Math.Max(width, height);

				width = Math.Max(1, (int)Math.Round(width * scale));
				height = Math.Max(1, (int)Math.Round(height * scale));
			}

			using (var scaled = new Bitmap(width, height))
			{
				using (var graphics = Graphics.FromImage(scaled))
				{
					graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
					graphics.SmoothingMode = SmoothingMode.HighQuality;
					graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
					graphics.CompositingQuality = CompositingQuality.HighQuality;

					using (var attributes = new ImageAttributes())
					{
						attributes.SetWrapMode(WrapMode.TileFlipXY);

						graphics.DrawImage(source, new Rectangle(0, 0, width, height), 0, 0, source.Width, source.Height, GraphicsUnit.Pixel, attributes);
					}
				}

				using (var stream = new MemoryStream())
				{
					var codec = ImageCodecInfo.GetImageEncoders().FirstOrDefault(c => c.FormatID == ImageFormat.Jpeg.Guid);

					if (codec is null)
					{
						scaled.Save(stream, ImageFormat.Jpeg);
					}
					else
					{
						using (var parameters = new EncoderParameters(1))
						{
							parameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, (long)JPEG_QUALITY);

							scaled.Save(stream, codec, parameters);
						}
					}

					return stream.ToArray();
				}
			}
		}

		/// <summary>
		/// Most of the key-less hosts answer with the plain url as text, some of them with json.
		/// </summary>
		private static string ExtractUrl(string response)
		{
			if (string.IsNullOrWhiteSpace(response))
				return "";

			var text = response.Trim();

			try
			{
				var json = JToken.Parse(text);

				return FindUrlInJson(json) ?? "";
			}
			catch
			{
				// not a json response, fall through
			}

			foreach (var line in text.Split('\n'))
			{
				var candidate = line.Trim();

				if (candidate.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
					return candidate;
			}

			return "";
		}

		private static string FindUrlInJson(JToken token)
		{
			switch (token.Type)
			{
				case JTokenType.Object:
					foreach (var property in (JObject)token)
					{
						var value = FindUrlInJson(property.Value);

						if (!string.IsNullOrWhiteSpace(value))
							return value;
					}

					return null;

				case JTokenType.Array:
					foreach (var item in (JArray)token)
					{
						var value = FindUrlInJson(item);

						if (!string.IsNullOrWhiteSpace(value))
							return value;
					}

					return null;

				case JTokenType.String:
					var text = token.ToString().Trim();

					return text.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ? text : null;

				default:
					return null;
			}
		}

		private void LoadCache()
		{
			try
			{
				if (!File.Exists(_cachePath))
					return;

				var loaded = JsonConvert.DeserializeObject<Dictionary<string, CacheEntry>>(File.ReadAllText(_cachePath));

				if (loaded is null)
					return;

				lock (_lock)
				{
					foreach (var pair in loaded)
					{
						if (!string.IsNullOrWhiteSpace(pair.Value?.url) && pair.Value.expires > DateTime.UtcNow)
						{
							_cache[pair.Key] = pair.Value;
						}
					}
				}
			}
			catch (Exception ex)
			{
				LogSync($"could not read the cover cache: {ex.Message}");
			}
		}

		private void SaveCache()
		{
			if (!_cacheEnabled)
				return;

			try
			{
				Dictionary<string, CacheEntry> snapshot;

				lock (_lock)
				{
					snapshot = new Dictionary<string, CacheEntry>(_cache);
				}

				var tempPath = _cachePath + ".tmp";

				File.WriteAllText(tempPath, JsonConvert.SerializeObject(snapshot));
				File.Copy(tempPath, _cachePath, true);
				File.Delete(tempPath);
			}
			catch (Exception ex)
			{
				LogSync($"could not write the cover cache: {ex.Message}");
			}
		}

		private async Task LogAsync(string message)
		{
			try
			{
				await _log.WriteLineAsync($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}");
				await _log.FlushAsync();
			}
			catch
			{
				// logging must never break the playback
			}
		}

		private void LogSync(string message)
		{
			try
			{
				_log.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}");
				_log.Flush();
			}
			catch
			{
				// logging must never break the playback
			}
		}

		public void Dispose()
		{
			SaveCache();

			_cache.Clear();
			_inWork.Clear();
			_failures.Clear();

			_fallback?.Dispose();
			_http.Dispose();
			_log.Dispose();
		}
	}
}