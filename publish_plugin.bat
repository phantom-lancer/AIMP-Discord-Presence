rmdir publish\AIMP-Discord-Presence-2 /q /s
rmdir publish\aimp_DiscordPresence2 /q /s
del publish\aimp_DiscordPresence2.zip

dotnet build AIMP-Discord-Presence-2 -c Release -p:Platform=x86 -o publish\aimp_DiscordPresence2

rem Compress-Archive writes "\" as the separator inside the zip entries, which is invalid per the
rem zip spec - AIMP rejects such packages with "invalid file format", so entries are written by hand
powershell -NoProfile -ExecutionPolicy Bypass -File publish_plugin_zip.ps1 -Source publish\aimp_DiscordPresence2 -Output publish\aimp_DiscordPresence2.zip

rmdir publish\aimp_DiscordPresence2 /q /s