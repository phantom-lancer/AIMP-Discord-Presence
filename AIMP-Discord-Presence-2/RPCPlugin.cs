using AIMP.SDK;
using AIMP.SDK.FileManager.Objects;
using AIMP_Discord_Presence_2.Config;
using AIMP_Discord_Presence_2.Services;
using DiscordRPC;
using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Xml;
using System.Xml.Serialization;

namespace AIMP_Discord_Presence_2
{
	[AimpPlugin("Discord Rich Presence 2", "BowieD", "0.0.3", AimpPluginType = AimpPluginType.Addons)]
	public class RPCPlugin : AimpPlugin
	{
		public PluginConfiguration Configuration { get; private set; } = new PluginConfiguration();

		private Timer _timer;
		private DiscordRpcClient _rpcClient;
		private RichPresence _presence;
		private IAlbumArtService _albumArtService;
		private readonly XmlSerializer _configSerializer = new XmlSerializer(typeof(PluginConfiguration));

		private void LoadConfig()
		{
			Configuration.LoadDefaults();

			var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

			var dir = Path.Combine(appData, "BowieD_AIMPDiscordPresence2");

			var path = Path.Combine(dir, "config.xml");

			if (File.Exists(path))
			{
				try
				{
					using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
					using (XmlReader reader = XmlReader.Create(fs))
					{
						Configuration = _configSerializer.Deserialize(reader) as PluginConfiguration;
					}

					if (Configuration is null)
					{
						Configuration = new PluginConfiguration();
						Configuration.LoadDefaults();
					}

					Configuration.SanityCheck();
				}
				catch
				{
					Configuration.LoadDefaults();
				}
			}
			else
			{
				if (!Directory.Exists(dir))
					Directory.CreateDirectory(dir);

				using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
				using (XmlWriter writer = XmlWriter.Create(fs))
				{
					_configSerializer.Serialize(writer, Configuration);
				}
			}
		}

		public override void Initialize()
		{
			LoadConfig();

			switch (Configuration.albumArtProvider)
			{
				case EAlbumArtProvider.Imgur when !string.IsNullOrWhiteSpace(Configuration.imgurClientId):
					_albumArtService = new ImgurAlbumArtService(Configuration.imgurClientId, Configuration.maxCacheCount, Configuration.automaticallyDeleteOnPluginShutdown, Configuration.automaticallyDeleteOnSongSwitch, Configuration.retryCount, Configuration.retryDelayMs);
					break;
				case EAlbumArtProvider.Discord:
					_albumArtService = new DiscordAlbumArtService();
					break;
				case EAlbumArtProvider.MusicBrainz when !string.IsNullOrWhiteSpace(Configuration.musicBrainzUserAgent):
					_albumArtService = new MusicBrainzAlbumArtService(Configuration.musicBrainzUserAgent);
					break;
				case EAlbumArtProvider.StaticWebsite:
					_albumArtService = new StaticWebsiteAlbumArtService(Configuration.staticWebsiteUrlFormat);
					break;
				case EAlbumArtProvider.Embedded:
					_albumArtService = new EmbeddedAlbumArtService(Configuration.embeddedUploadEndpoint, Configuration.embeddedUploadExpiry, Configuration.embeddedMaxDimension, Configuration.embeddedMinDimension, Configuration.embeddedInternetFallback, Configuration.embeddedFallbackUserAgent, Configuration.retryCount, Configuration.retryDelayMs);
					break;
				default:
					_albumArtService = new PlaceholderAlbumArtService();
					break;
			}

			_rpcClient = new DiscordRpcClient(Configuration.discordApplicationId, autoEvents: true);
			_rpcClient.Initialize();
			_rpcClient.SetPresence(_presence);

			_timer = new Timer(OnTimerCallback, null, TimeSpan.Zero, TimeSpan.FromSeconds(Configuration.updateFrequency));
		}

		private void OnTimerCallback(object state)
		{
			try
			{
				var plrSrv = this.Player.ServicePlayer;

				UpdateTrackInfo(plrSrv.CurrentFileInfo);
			}
			catch { }
		}

		public override void Dispose()
		{
			_timer?.Dispose();
			_timer = null;
			_rpcClient?.Deinitialize();
			_rpcClient?.Dispose();
			_rpcClient = null;
			_presence = null;
			_albumArtService?.Dispose();
			_albumArtService = null;
		}

		public void UpdateTrackInfo()
		{
			this.UpdateTrackInfo(this.Player.ServicePlayer.CurrentFileInfo);
		}
		private string GetSearchQuery(IAimpFileInfo aimpFile)
		{
			string query = $"\"{aimpFile.Title}\" by \"{aimpFile.Artist}\"";

			return WebUtility.UrlEncode(query);
		}
		private static string Clamp(string value, int maxLength)
		{
			if (string.IsNullOrWhiteSpace(value))
				return "";

			value = value.Trim();

			return value.Length > maxLength ? value.Substring(0, maxLength) : value;
		}

		public void UpdateTrackInfo(IAimpFileInfo aimpFile)
		{
			var title = Clamp(aimpFile.Title, 127);
			var album = Clamp(aimpFile.Album, 127);

			_presence = new RichPresence()
			{
				Details = title,
				State = Clamp(aimpFile.Artist, 127),
				Assets = new Assets()
				{
					LargeImageKey = "aimp_logo",
					// Discord renders this both as a third line under the state and as the hover text of
					// the big image, so an album repeating the title is skipped to avoid the duplicate line
					LargeImageText = album.Equals(title, StringComparison.OrdinalIgnoreCase) ? "" : album,
				},
				Timestamps = new Timestamps()
				{

				},
				Type = ActivityType.Listening,
			};

			if (Configuration.displaySmallLogo)
			{
				_presence.Assets.SmallImageKey = "aimp_logo";

				// the album is only shown on hover here, the big image text is reserved for the
				// third line Discord draws under the state
				_presence.Assets.SmallImageText = album.Length > 0 ? album : "AIMP";
			}

			var plrSrv = this.Player.ServicePlayer;

			if (plrSrv.State != AimpPlayerState.Stopped)
			{
				if (plrSrv.State == AimpPlayerState.Playing)
				{
					double duration = plrSrv.Duration;

					if (duration != 0)
					{
						var pos = plrSrv.Position;

						TimeSpan posTs = TimeSpan.FromSeconds(pos);
						_presence.Timestamps.Start = DateTime.UtcNow.Subtract(posTs);
						_presence.Timestamps.End = DateTime.UtcNow.AddSeconds(duration - pos);
					}
				}

				var url = _albumArtService.TryGetImageUrl(aimpFile);
				if (!string.IsNullOrWhiteSpace(url))
				{
					_presence.Assets.LargeImageKey = url;
				}
			}

			if (plrSrv.State != AimpPlayerState.Playing)
			{
				_presence.Assets.SmallImageKey = "aimp_paused";
			}

			if (Configuration.addPresenceButtons)
			{
				string songSearchUrl = $"https://www.youtube.com/results?search_query={GetSearchQuery(aimpFile)}";

				songSearchUrl = songSearchUrl.Substring(0, Math.Min(127, songSearchUrl.Length));

				if (string.IsNullOrWhiteSpace(aimpFile.URL))
				{
					_presence.Buttons = new Button[1]
					{
						new Button()
						{
							Label = "Search on YouTube",
							Url = songSearchUrl,
						},
					};
				}
				else
				{
					_presence.Buttons = new Button[2]
					{
						new Button()
						{
							Label = "Open Song URL",
							Url = aimpFile.URL,
						},
						new Button()
						{
							Label = "Search on YouTube",
							Url = songSearchUrl,
						},
					};
				}
			}
			else
			{
				_presence.Buttons = Array.Empty<Button>();
			}

			_rpcClient.SetPresence(_presence);
		}
	}
}
