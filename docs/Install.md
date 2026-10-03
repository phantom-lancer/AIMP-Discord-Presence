# Установка

## Что нужно

- [AIMP 5](https://www.aimp.ru) для Windows x86 (32-бит)
- [.NET Framework 4.8](https://dotnet.microsoft.com/en-us/download/dotnet-framework/net48)

## Вариант 1. Через сам плеер

1. Скачать архив `aimp_DiscordPresence2.zip`.
2. В AIMP открыть **Plugins** (главное меню → Plugins).
3. Нажать **Install** и выбрать скачанный архив.
4. Если плагин не подсветился автоматически — найти его в списке и включить галочкой.
5. Перезапустить AIMP, если он был открыт.

## Вариант 2. Руками

1. Распаковать архив.
2. Скопировать папку `aimp_DiscordPresence2` в `C:\Program Files (x86)\AIMP\Plugins\`.
3. В AIMP открыть **Plugins**, найти **Discord Rich Presence 2**, включить галочкой.

## Структура папки плагина

```
Plugins\aimp_DiscordPresence2\
    aimp_DiscordPresence2.dll      # копия aimp_dotnet.dll, точка входа модуля для AIMP
    AIMP-Discord-Presence-2.dll    # сам плагин, собирается из этого репозитория
    aimp_dotnet.dll                # мост AIMP DotNet
    AIMP.SDK.dll
    Newtonsoft.Json.dll
    DiscordRPC.dll
```

`aimp_dotnet.dll` — это только мост: AIMP грузит плагин через его копию, названную именем
плагина. Без файла `aimp_DiscordPresence2.dll` AIMP молча игнорирует весь модуль, и плагина
не будет в списке вообще. `publish_plugin_zip.ps1` создаёт эту копию автоматически.

Если плеер 64-битный, нужны 64-битные версии AIMP SDK из nuget-пакета `AimpSDK-x64`.

## Настройка

GUI-настроек у плагина нет. Всё лежит в файле
`%AppData%\BowieD_AIMPDiscordPresence2\config.xml`.

Удалите файл — при следующем запуске AIMP создаст его с настройками по умолчанию (провайдер
обложек `Embedded`, кнопки и маленькая иконка включены).

Провайдер обложек выбирается строкой `albumArtProvider`: `Embedded`, `Imgur`, `Discord`,
`MusicBrainz`, `StaticWebsite`, `None`. Подробности — в [Compare.md](Compare.md).

## Если ничего не работает

- Плагина нет в списке — проверьте, что рядом с `AIMP-Discord-Presence-2.dll` лежит файл
  `aimp_DiscordPresence2.dll` (см. выше).
- Статус в Discord не появляется — в самом Discord включено «Show current session as a game
  presence» в настройках пользователя.
- Лог загрузки обложек: `%AppData%\BowieD_AIMPDiscordPresence2\EmbeddedProvider\uploads.log`
  (создаётся только провайдером Embedded).

## Сборка из исходников

Нужен только .NET SDK, Visual Studio не обязательна.

```powershell
nuget restore AIMP-Discord-Presence-2.sln
dotnet build AIMP-Discord-Presence-2 -c Release -p:Platform=x86 -o publish\aimp_DiscordPresence2
powershell -File publish_plugin_zip.ps1 -Source publish\aimp_DiscordPresence2 -Output publish\aimp_DiscordPresence2.zip
```

`-p:Platform=x86` обязателен: AIMP — 32-битное приложение, AnyCPU-сборка не загрузится.

`publish_all.bat` собирает плагин и вспомогательный AlbumCoverGatherer, `publish_plugin.bat`
только плагин. Упаковка идёт через `publish_plugin_zip.ps1`: `Compress-Archive` пишет в zip
разделитель `\`, что не соответствует спецификации, и AIMP отвергает такой архив с
«invalid file format».