using AIMP.SDK.FileManager.Objects;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace AIMP_Discord_Presence_2.Services
{
	/// <summary>
	/// Looks the cover up by album name. Used as a last resort for files without embedded artwork,
	/// so it never blocks: the request runs in the background and the url is returned from the cache
	/// once it is known.
	/// </summary>
	public sealed class MusicBrainzAlbumArtService : IAlbumArtService
	{
		public const int DEFAULT_TIMEOUT_SECONDS = 6;
		public const int DEFAULT_FAILURE_LIMIT = 3;

		public sealed class ReleaseGroupMetadata
		{
			[JsonProperty("release-groups")]
			public List<ReleaseGroup> releaseGroups;
		}
		public sealed class ReleaseGroup
		{
			[JsonProperty("releases")]
			public List<Release> releases;
		}
		public sealed class Release
		{
			[JsonProperty("id")]
			public string id;
		}
		public sealed class CovertImages
		{
			[JsonProperty("images")]
			public List<CovertImage> images;
		}
		public sealed class CovertImage
		{
			[JsonProperty("image")]
			public string image;
			[JsonProperty("back")]
			public bool back;
			[JsonProperty("front")]
			public bool front;
		}

		private readonly HttpClient _http;
		private readonly StreamWriter _log;
		private readonly int _failureLimit;
		private readonly object _lock = "";
		private readonly Dictionary<string, string> _cache = new Dictionary<string, string>();
		private readonly HashSet<string> _inWork = new HashSet<string>();

		private int _consecutiveFailures;
		private bool _disabled;

		public MusicBrainzAlbumArtService(string musicBrainzUserAgent, int timeoutSeconds = DEFAULT_TIMEOUT_SECONDS, int failureLimit = DEFAULT_FAILURE_LIMIT)
		{
			EmbeddedAlbumArtService.EnsureModernTls();

			_http = new HttpClient
			{
				// MusicBrainz drops the connection on networks that block it, without this the presence
				// update would sit behind a request that hangs for the default 100 seconds.
				Timeout = TimeSpan.FromSeconds(timeoutSeconds < 1 ? DEFAULT_TIMEOUT_SECONDS : timeoutSeconds),
			};
			_http.DefaultRequestHeaders.Add("User-Agent", string.IsNullOrWhiteSpace(musicBrainzUserAgent) ? EmbeddedAlbumArtService.DEFAULT_USER_AGENT : musicBrainzUserAgent.Trim());

			_failureLimit = failureLimit < 1 ? DEFAULT_FAILURE_LIMIT : failureLimit;

			var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BowieD_AIMPDiscordPresence2", "MusicBrainzProvider");

			if (!Directory.Exists(dir))
			{
				Directory.CreateDirectory(dir);
			}

			_log = new StreamWriter(new FileStream(Path.Combine(dir, "musicbrainz.log"), FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
			{
				AutoFlush = true,
			};
		}

		private static string SanitizeForUrl(string value)
		{
			return WebUtility.UrlEncode(value ?? "");
		}

		private static string ComputeKey(IAimpFileInfo fileInfo)
		{
			var artist = string.IsNullOrWhiteSpace(fileInfo.AlbumArtist) ? fileInfo.Artist : fileInfo.AlbumArtist;

			return $"{fileInfo.Album}|{artist}";
		}

		public string TryGetImageUrl(IAimpFileInfo fileInfo)
		{
			if (fileInfo is null)
				return "";

			if (string.IsNullOrWhiteSpace(fileInfo.Album))
				return "";

			var key = ComputeKey(fileInfo);

			lock (_lock)
			{
				if (_disabled)
					return "";

				if (_cache.TryGetValue(key, out var cached))
					return cached;

				if (!_inWork.Add(key))
					return "";
			}

			Task.Run(async () => await ResolveAsync(key, fileInfo));

			return "";
		}

		private async Task ResolveAsync(string key, IAimpFileInfo fileInfo)
		{
			string url = "";

			try
			{
				url = await LookupAsync(fileInfo);
			}
			catch (Exception ex)
			{
				await LogAsync($"could not look up \"{key}\": {ex.Message}");
			}

			bool disable = false;

			lock (_lock)
			{
				_inWork.Remove(key);
				_cache[key] = url;

				if (string.IsNullOrWhiteSpace(url) && ++_consecutiveFailures >= _failureLimit)
				{
					_disabled = true;
					disable = true;
				}
				else if (!string.IsNullOrWhiteSpace(url))
				{
					_consecutiveFailures = 0;
				}
			}

			if (!string.IsNullOrWhiteSpace(url))
			{
				await LogAsync($"\"{key}\" -> {url}");
			}

			if (disable)
			{
				await LogAsync($"disabled for this session after {_consecutiveFailures} failed lookups, MusicBrainz looks unreachable");
			}
		}

		private async Task<string> LookupAsync(IAimpFileInfo fileInfo)
		{
			var artist = string.IsNullOrWhiteSpace(fileInfo.AlbumArtist) ? fileInfo.Artist : fileInfo.AlbumArtist;

			var content = await _http.GetStringAsync($"https://musicbrainz.org/ws/2/release-group?query={SanitizeForUrl(fileInfo.Album)} {SanitizeForUrl(artist)}&inc=aliases&fmt=json&limit=1");

			var metadata = JsonConvert.DeserializeObject<ReleaseGroupMetadata>(content);

			if (metadata?.releaseGroups is null || metadata.releaseGroups.Count == 0)
				return "";

			var releaseId = metadata.releaseGroups[0].releases[0].id;

			content = await _http.GetStringAsync($"http://coverartarchive.org/release/{releaseId}");

			var images = JsonConvert.DeserializeObject<CovertImages>(content);

			if (images?.images is null || images.images.Count == 0)
				return "";

			string imageUrl;

			try
			{
				imageUrl = images.images.First(d => d.front).image;
			}
			catch
			{
				imageUrl = images.images.First().image;
			}

			// Cover Art Archive redirects to the real image, follow it so Discord gets a direct link.
			var response = await _http.GetAsync(imageUrl);

			return response.RequestMessage.RequestUri.OriginalString;
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
			_inWork.Clear();

			_http.Dispose();
			_log.Dispose();
		}
	}
}