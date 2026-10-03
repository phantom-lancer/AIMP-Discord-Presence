# Installation

1. Install the [.NET Framework 4.8 runtime](https://dotnet.microsoft.com/en-us/download/dotnet-framework/net48) (required).

2. Download the plugin archive (`aimp_DiscordPresence2.zip`).

3. Open AIMP and go to **Plugins** (main menu -> Plugins, or the "Plugins" item in the player).

4. Press **Install**, select the downloaded `aimp_DiscordPresence2.zip`.

5. Go back to the plugin list and **enable** "Discord Rich Presence 2".

6. Restart AIMP if it was already running.

Everything else is configured in `%AppData%\BowieD_AIMPDiscordPresence2\config.xml`.
Delete that file to get the defaults back (a fresh one is written on the next start).

## Setting up album covers

Pick your way to install [here](Compare.md). The default provider is
[Embedded](Embedded.md): the cover is taken from the track file itself, no api keys needed.

## Building it yourself

```
nuget restore AIMP-Discord-Presence-2.sln
dotnet build AIMP-Discord-Presence-2 -c Release -p:Platform=x86 -o publish\aimp_DiscordPresence2
```

AIMP is a 32 bit application, so the plugin has to be built for x86 (`-p:Platform=x86`).
The result is a single `AIMP-Discord-Presence-2.dll` that goes next to `DiscordRPC.dll`,
`AIMP.SDK.dll`, `aimp_dotnet.dll` and `Newtonsoft.Json.dll` in the plugin folder.