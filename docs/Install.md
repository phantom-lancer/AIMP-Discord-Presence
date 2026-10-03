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

## Plugin folder structure

AIMP loads `aimp_DiscordPresence2.dll` as the module, and that file has to be a **copy of
`aimp_dotnet.dll`** - it is the entry point of the AIMP DotNet bridge, not the plugin itself
(this is what the committed `aimp_DiscordPresence2.dll` in `AIMP-Discord-Presence-2` is for).
AIMP silently ignores the whole plugin when that file is missing.

```
Plugins\aimp_DiscordPresence2\
    aimp_DiscordPresence2.dll      <- copy of aimp_dotnet.dll, the AIMP module
    AIMP-Discord-Presence-2.dll    <- the plugin itself, built from this repository
    aimp_dotnet.dll                <- AIMP DotNet bridge
    AIMP.SDK.dll
    Newtonsoft.Json.dll
    DiscordRPC.dll
```