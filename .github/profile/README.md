## О проекте

AIMP Discord Presence 2 — плагин для AIMP, показывающий текущий трек в Discord Rich
Presence. Обложка трека берётся из самого файла и загружается на анонимный хостинг без
ключей API, поэтому MusicBrainz, Imgur и Discord-приложение не нужны.

## Возможности

- обложка из тегов файла (mp3/flac/m4a), без поиска по интернету
- загрузка без API-ключей, без аккаунтов, с автоудалением
- альбом в статусе и в тултипах, таймлайн, иконка паузы, кнопки поиска
- пять провайдеров обложек на выбор: Embedded, Imgur, Discord, MusicBrainz, StaticWebsite
- конфигурация в текстовом файле, без GUI-диалогов

## Установка

Архив `aimp_DiscordPresence2.zip` ставится через AIMP: Plugins → Install. Подробности в
[docs/Install.md](docs/Install.md), провайдеры обложек — в
[docs/Compare.md](docs/Compare.md).

## Сборка

Требуется только .NET SDK:

```powershell
nuget restore AIMP-Discord-Presence-2.sln
dotnet build AIMP-Discord-Presence-2 -c Release -p:Platform=x86 -o publish\aimp_DiscordPresence2
```

Сборка обязана быть x86: AIMP — 32-битное приложение.

## Документация

- [Установка](docs/Install.md)
- [Сравнение провайдеров обложек](docs/Compare.md)
- [Embedded: обложка из файла](docs/Embedded.md)
- [Imgur](docs/Imgur.md) · [Discord](docs/Discord.md) · [MusicBrainz](docs/MusicBrainz.md) · [StaticWebsite](docs/StaticWebsite.md)
- [ contributing / Участие в проекте](CONTRIBUTING.md)

## Ограничения

- Discord показывает только картинку по публичному https-ссылке, локальный файл
  показать нельзя — обложку нужно куда-то загрузить.
- Обложки в mp3 есть далеко не всегда; в этом случае включается откат на MusicBrainz.
- Настроек в GUI нет, всё в `%AppData%\BowieD_AIMPDiscordPresence2\config.xml`.