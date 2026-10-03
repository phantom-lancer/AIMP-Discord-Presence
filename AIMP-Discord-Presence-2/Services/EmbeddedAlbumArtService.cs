using AIMP.SDK.FileManager.Objects;
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
	/// </summary>
	public sealed class EmbeddedAlbumArtService : IAlbumArtService
	{
		public const string DEFAULT_UPLOAD_ENDPOINT = "https://litterbox.catbox.moe/resources/internals/api.php";
		public const string DEFAULT_UPLOAD_EXPIRY = "1h";
		public const string DEFAULT_USER_AGENT = "AIMP-Discord-Presence-2/0.0.3";

		private const int JPEG_QUALITY = 90;
		private static readonly TimeSpan FAILURE_COOLDOWN = TimeSpan.FromMinutes(5);

		private readonly HttpClient _http;
		private readonly StreamWriter _log;

		private readonly string _uploadEndpoint;
		private readonly string _uploadExpiry;
		private readonly int _maxDimension;
		private readonly int _minDimension;
		private readonly int _retryCount;
		private readonly int _retryDelay;

		private readonly object _lock = "";
		private readonly Dictionary<string, string> _cache = new Dictionary<string, string>();
		private readonly Dictionary<string, DateTime> _cooldowns = new Dictionary<string, DateTime>();
		private readonly HashSet<string> _inWork = new HashSet<string>();

		private readonly IAlbumArtService _fallback;

		public EmbeddedAlbumArtService(string uploadEndpoint, string uploadExpiry, int maxDimension, int minDimension, bool useInternetFallback, string fallbackUserAgent, int retryCount, int retryDelay)
		{
			EnsureModernTls();

			_http = new HttpClient
			{
				Timeout = TimeSpan.FromSeconds(30),
			};
			_http.DefaultRequestHeaders.Add("User-Agent", "AIMP-Discord-Presence-2");

			_uploadEndpoint = string.IsNullOrWhiteSpace(uploadEndpoint) ? DEFAULT_UPLOAD_ENDPOINT : uploadEndpoint.Trim();
			_uploadExpiry = string.IsNullOrWhiteSpace(uploadExpiry) ? DEFAULT_UPLOAD_EXPIRY : uploadExpiry.Trim();
			_maxDimension = maxDimension < 64 ? 512 : maxDimension;
			_minDimension = minDimension < 16 ? 16 : minDimension;
			_retryCount = retryCount < 1 ? 1 : retryCount;
			_retryDelay = retryDelay < 100 ? 500 : retryDelay;

			// Covers are almost never embedded into mp3s, so an internet lookup may be used as a last resort.
			_fallback = useInternetFallback
				? new MusicBrainzAlbumArtService(string.IsNullOrWhiteSpace(fallbackUserAgent) ? DEFAULT_USER_AGENT : fallbackUserAgent.Trim())
				: null;

			var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BowieD_AIMPDiscordPresence2", "EmbeddedProvider");

			if (!Directory.Exists(dir))
			{
				Directory.CreateDirectory(dir);
			}

			_log = new StreamWriter(new FileStream(Path.Combine(dir, "uploads.log"), FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
			{
				AutoFlush = true,
			};
		}

		/// <summary>
		/// AIMP pulls in an old ServicePointManager default (SSL3/TLS1.0), and every https host
		/// refuses to talk to it, which surfaces as a bare "an error occurred while sending the
		/// request" from HttpClient.
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

		private static string ComputeKey(IAimpFileInfo fileInfo)
		{
			using (var sha1 = SHA1.Create())
			{
				var bytes = Encoding.UTF8.GetBytes($"{fileInfo.FileName}|{fileInfo.FileSize}");

				return string.Join("", sha1.ComputeHash(bytes).Select(d => d.ToString("x2")));
			}
		}

		public string TryGetImageUrl(IAimpFileInfo fileInfo)
		{
			if (fileInfo is null)
				return "";

			var art = fileInfo.AlbumArt;

			// Most mp3 files carry either nothing or a 1x1 placeholder image, treat both as "no cover".
			if (art is null ||
				art.Width < _minDimension ||
				art.Height < _minDimension)
			{
				return TryGetFallbackUrl(fileInfo);
			}

			var key = ComputeKey(fileInfo);

			lock (_lock)
			{
				if (_cooldowns.TryGetValue(key, out var retryAt) && DateTime.UtcNow < retryAt)
					return TryGetFallbackUrl(fileInfo);

				if (_cache.TryGetValue(key, out var cachedUrl))
					return cachedUrl;

				if (!_inWork.Add(key))
					return "";
			}

			Task.Run(async () => await UploadCoverAsync(key, art));

			return "";
		}

		private string TryGetFallbackUrl(IAimpFileInfo fileInfo)
		{
			return _fallback?.TryGetImageUrl(fileInfo) ?? "";
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
					await LogAsync($"[attempt #{i + 1}] could not upload the cover: {ex}");

					await Task.Delay(_retryDelay);
				}
			}

			lock (_lock)
			{
				_inWork.Remove(key);

				if (string.IsNullOrWhiteSpace(url))
				{
					_cooldowns[key] = DateTime.UtcNow.Add(FAILURE_COOLDOWN);
					return;
				}

				_cache[key] = url;
			}
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

		public void Dispose()
		{
			_cache.Clear();
			_cooldowns.Clear();
			_inWork.Clear();

			_http.Dispose();
			_fallback?.Dispose();
			_log.Dispose();
		}
	}
}