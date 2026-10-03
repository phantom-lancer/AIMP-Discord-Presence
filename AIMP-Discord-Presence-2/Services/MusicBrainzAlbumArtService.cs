using AIMP.SDK.FileManager.Objects;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;

namespace AIMP_Discord_Presence_2.Services
{
	public sealed class MusicBrainzAlbumArtService : IAlbumArtService
	{
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

		private string _prevAlbum;
		private string _prevResult;

		private readonly HttpClient _http;

		public MusicBrainzAlbumArtService(string musicBrainzUserAgent)
		{
			EmbeddedAlbumArtService.EnsureModernTls();

			_http = new HttpClient();
			_http.DefaultRequestHeaders.Add("User-Agent", musicBrainzUserAgent);
		}

		private static string SanitizeForUrl(string value)
		{
			return WebUtility.UrlEncode(value);
		}

		public string TryGetImageUrl(IAimpFileInfo fileInfo)
		{
			if (fileInfo.Album == _prevAlbum)
				return _prevResult;

			try
			{
				string releaseId;
				{
					string artist;

					if (string.IsNullOrWhiteSpace(fileInfo.AlbumArtist))
					{
						artist = fileInfo.Artist;
					}
					else
					{
						artist = fileInfo.AlbumArtist;
					}

					var content = _http.GetStringAsync($"https://musicbrainz.org/ws/2/release-group?query={SanitizeForUrl(fileInfo.Album)} {SanitizeForUrl(artist)}&inc=aliases&fmt=json&limit=1").ConfigureAwait(false).GetAwaiter().GetResult();

					var metadata = JsonConvert.DeserializeObject<ReleaseGroupMetadata>(content);

					releaseId = metadata.releaseGroups[0].releases[0].id;
				}

				string imageUrl;
				{
					var content = _http.GetStringAsync($"http://coverartarchive.org/release/{releaseId}").ConfigureAwait(false).GetAwaiter().GetResult();

					var metadata = JsonConvert.DeserializeObject<CovertImages>(content);

					try
					{
						imageUrl = metadata.images.First(d => d.front).image;
					}
					catch
					{
						imageUrl = metadata.images.First().image;
					}
				}

				string realImageUrl;
				{
					var response = _http.GetAsync(imageUrl).ConfigureAwait(false).GetAwaiter().GetResult();

					realImageUrl = response.RequestMessage.RequestUri.OriginalString;
				}

				_prevResult = realImageUrl;

				_prevAlbum = fileInfo.Album;
				return _prevResult;
			}
			catch
			{
				_prevAlbum = fileInfo.Album;
				return _prevResult;
			}
		}

		public void Dispose()
		{
			_http.Dispose();
		}
	}
}
