# Embedded (cover taken from the track file)

The default provider of this fork. It takes the cover **embedded into the track file itself**
(`APIC` in mp3, `PICTURE` in flac, `covr` in m4a), scales it down to
`embeddedMaxDimension` and uploads it to a plain file host, because Discord Rich Presence
is only able to display images available by an **https url** (a local path or `file://` will
never work, no matter what you feed into `LargeImageKey`).

## Why an upload is needed

The Discord client downloads the image on its own, so the plugin has to hand it a public
https address. The embedded provider uses [litterbox](https://litterbox.catbox.moe), which
needs **no api key, no account and no registration** - the upload is anonymous and the
uploaded file is deleted automatically.

## Settings

All of them live in `%AppData%\BowieD_AIMPDiscordPresence2\config.xml`:

| Setting                   | Default                                                | Meaning                                                        |
| ------------------------- | ------------------------------------------------------ | -------------------------------------------------------------- |
| `albumArtProvider`        | `Embedded`                                             | `Embedded` for this provider                                    |
| `embeddedUploadEndpoint`  | `https://litterbox.catbox.moe/resources/internals/api.php` | Where the cover is uploaded to                                  |
| `embeddedUploadExpiry`    | `1h`                                                   | How long the uploaded cover lives (`1h`, `12h`, `24h`, `48h`, `72h`) |
| `embeddedMaxDimension`    | `512`                                                  | Covers bigger than this are scaled down before uploading        |
| `embeddedMinDimension`    | `100`                                                  | Covers smaller than this are treated as "no cover"              |
| `embeddedInternetFallback`| `true`                                                 | If the file has no cover, look it up on MusicBrainz             |
| `embeddedFallbackUserAgent` | `AIMP-Discord-Presence-2/0.0.3`                      | User agent used by the MusicBrainz fallback                      |

Logs of every upload are written to
`%AppData%\BowieD_AIMPDiscordPresence2\EmbeddedProvider\uploads.log`.

## Nothing embedded? (the mp3 case)

Almost every mp3 in the wild has **no** embedded cover - either no `APIC` frame at all, or a
1x1 placeholder. Such covers are rejected by `embeddedMinDimension` and:

* if `embeddedInternetFallback` is `true` (default) the cover is looked up on MusicBrainz and
  used directly, no upload involved;
* if you want strictly local covers - set it to `false`, and embed the artwork into your files
  once (for example with Mp3tag: select all tracks -> "Tag pictures" -> pick the folder with
  the covers). Flac files with embedded pictures work without any fallback at all.

Uploads are cached per track (file name + size), so switching back and forth between songs does
not re-upload the same cover over and over.