using AIMP_Discord_Presence_2.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace AIMP_Discord_Presence_2.Config
{
	public sealed class PluginConfiguration
	{
		// Common
		public string discordApplicationId;
		[JsonConverter(typeof(StringEnumConverter))]
		public EAlbumArtProvider albumArtProvider;
		public double updateFrequency;
		public double statePollInterval;
		public bool addPresenceButtons;
		public bool displaySmallLogo;
		public int retryCount;
		public int retryDelayMs;
		public bool coverCacheEnabled;
		public bool coverPrefetchEnabled;

		// Imgur
		public string imgurClientId;
		public bool automaticallyDeleteOnSongSwitch;
		public bool automaticallyDeleteOnPluginShutdown;
		public int maxCacheCount;

		// MusicBrainz
		public string musicBrainzUserAgent;

		// Static Website
		public string staticWebsiteUrlFormat;

		// Embedded cover (taken from the track file itself)
		public string embeddedUploadEndpoint;
		public string embeddedUploadExpiry;
		public int embeddedMaxDimension;
		public int embeddedMinDimension;
		public bool embeddedInternetFallback;
		public string embeddedFallbackUserAgent;

		public void LoadDefaults()
		{
			discordApplicationId = "429559336982020107";
			albumArtProvider = EAlbumArtProvider.Embedded;
			updateFrequency = 10.0;
			statePollInterval = 0.3;
			addPresenceButtons = true;
			displaySmallLogo = true;
			retryCount = 5;
			retryDelayMs = 500;
			coverCacheEnabled = true;
			coverPrefetchEnabled = true;

			imgurClientId = "";
			automaticallyDeleteOnPluginShutdown = true;
			automaticallyDeleteOnSongSwitch = false;
			maxCacheCount = 4;

			musicBrainzUserAgent = "";

			staticWebsiteUrlFormat = "";

			embeddedUploadEndpoint = EmbeddedAlbumArtService.DEFAULT_UPLOAD_ENDPOINT;
			embeddedUploadExpiry = EmbeddedAlbumArtService.DEFAULT_UPLOAD_EXPIRY;
			embeddedMaxDimension = 512;
			embeddedMinDimension = 100;
			embeddedInternetFallback = true;
			embeddedFallbackUserAgent = EmbeddedAlbumArtService.DEFAULT_USER_AGENT;
		}

		public void SanityCheck()
		{
			maxCacheCount = System.Math.Max(maxCacheCount, 2);
			updateFrequency = System.Math.Max(updateFrequency, 1);
			statePollInterval = statePollInterval < 0.1 ? 0.3 : System.Math.Min(statePollInterval, 5);
			retryCount = System.Math.Max(retryCount, 1);
			retryDelayMs = System.Math.Max(retryDelayMs, 100);
			embeddedMaxDimension = embeddedMaxDimension < 64 ? 512 : System.Math.Min(embeddedMaxDimension, 1024);

			// Covers smaller than that are the 1x1 placeholders mp3 files are usually shipped with.
			embeddedMinDimension = embeddedMinDimension < 16 ? 100 : System.Math.Min(embeddedMinDimension, 512);

			if (string.IsNullOrWhiteSpace(embeddedFallbackUserAgent))
			{
				embeddedFallbackUserAgent = EmbeddedAlbumArtService.DEFAULT_USER_AGENT;
			}

			if (string.IsNullOrWhiteSpace(embeddedUploadEndpoint))
			{
				embeddedUploadEndpoint = EmbeddedAlbumArtService.DEFAULT_UPLOAD_ENDPOINT;
			}

			if (string.IsNullOrWhiteSpace(embeddedUploadExpiry))
			{
				embeddedUploadExpiry = EmbeddedAlbumArtService.DEFAULT_UPLOAD_EXPIRY;
			}
		}
	}

	public enum EAlbumArtProvider
	{
		None = 0,
		Imgur = 1,
		Discord = 2,
		MusicBrainz = 3,
		StaticWebsite = 4,
		Embedded = 5,
	}
}
