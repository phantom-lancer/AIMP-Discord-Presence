# AIMP Discord Presence 2

Плагин для [AIMP](https://www.aimp.ru), который показывает то, что сейчас играет, в статусе
Discord Rich Presence.

Форк [iBowie/AIMP-Discord-Presence-2](https://github.com/iBowie/AIMP-Discord-Presence-2).
Главное отличие: **обложка трека берётся из самого файла**, а не ищется в интернете по названию
альбома — см. [Embedded](docs/Embedded.md).

## Что он делает

| Строка в Discord        | Что показывает                                        |
| ----------------------- | ----------------------------------------------------- |
| Заголовок               | «Слушает AIMP»                                        |
| 1-я строка              | название трека                                         |
| 2-я строка              | исполнитель                                            |
| 3-я строка              | альбом (скрывается, если он совпадает с названием)     |
| Большая картинка        | обложка трека из файла                                 |
| Тултип маленькой иконки | альбом                                                 |
| Кнопки                  | «Search on YouTube», «Open Song URL»                  |

Дополнительно: таймлайн с позицией и длительностью, иконка паузы, кнопки поиска.

## Провайдеры обложек

| Провайдер                             | Обложка из файла | Нужен ключ API | Комментарий                       |
| ------------------------------------- | ---------------- | -------------- | --------------------------------- |
| [Embedded](docs/Embedded.md)          | ✅               | ❌              | **по умолчанию**, загрузка на хост без регистрации |
| [Imgur](docs/Imgur.md)                | ✅               | ✅ Client ID    | вечные ссылки                     |
| [Discord](docs/Discord.md)            | ❌               | ручная загрузка| 298 ассетов на всё приложение     |
| [MusicBrainz](docs/MusicBrainz.md)    | ❌               | User-Agent     | ищет обложку по альбому в сети    |
| [StaticWebsite](docs/StaticWebsite.md)| ❌               | свой сайт      | для готовой коллекции обложек     |

Подробности и сравнение — в [docs/Compare.md](docs/Compare.md).

## Установка

Подробная инструкция — в [docs/Install.md](docs/Install.md). Коротко:

1. Установить [.NET Framework 4.8](https://dotnet.microsoft.com/en-us/download/dotnet-framework/net48).
2. Скачать `aimp_DiscordPresence2.zip`.
3. В AIMP открыть **Plugins** → **Install** → выбрать архив.
4. В списке плагинов включить **Discord Rich Presence 2**.

## Настройка

GUI-настроек у плагина нет, всё лежит в
`%AppData%\BowieD_AIMPDiscordPresence2\config.xml`. Удалите файл — при следующем запуске
AIMP создаст его с настройками по умолчанию.

| Параметр                     | По умолчанию                | Смысл                                             |
| ---------------------------- | --------------------------- | ------------------------------------------------- |
| `statePollInterval`          | `0.3`                       | как часто опрашивается состояние плеера, секунд   |
| `updateFrequency`            | `10`                        | страховочный таймер полной отправки, секунд        |
| `albumArtProvider`           | `Embedded`                  | чем берём обложку                                 |
| `embeddedUploadEndpoint`     | litterbox.catbox.moe        | куда заливаем обложку (ключ не нужен)              |
| `embeddedUploadExpiry`       | `1h`                        | через сколько удалится загруженная картинка        |
| `embeddedMaxDimension`       | `512`                       | до какого размера ужимать обложку                 |
| `embeddedMinDimension`       | `100`                       | меньше — считается заглушкой, игнорируется         |
| `embeddedInternetFallback`   | `true`                      | если обложки в файле нет, искать в MusicBrainz     |
| `coverCacheEnabled`          | `true`                      | помнить обложки на диске между запусками           |
| `coverPrefetchEnabled`       | `true`                      | качать обложку следующего трека заранее            |
| `displaySmallLogo`           | `true`                      | маленькая иконка AIMP (на её тултип вешается альбом)|
| `addPresenceButtons`         | `true`                      | кнопки под статусом                                |

## Почему переключение трека мгновенное

У AIMP нет событий для смены трека и паузы, поэтому состояние опрашивается
`statePollInterval` раз в секунду. Опрос читает только состояние плеера и имя текущего
файла — два дешёвых вызова, ни сети, ни картинок. Если ничего не изменилось, отправки в
Discord не происходит вовсе.

Всё, что требует сети или диска, уведено из этого пути: обложка, которая ещё не загружена,
просто не показывается и догружается в фоне, название трека доходит до Discord сразу.
Поэтому MusicBrainz с таймаутом 6 секунд и автоотключением после трёх неудач больше не может
задерживать статус.

Прогресс-бар перерисовывает сам Discord по меткам начала и конца, поэтому частые отправки ему
не нужны — позиция попадает в отправку один раз в 5 секунд и сразу после перемотки.

## Сборка из исходников

Нужен только .NET SDK; Visual Studio не обязательна.

```powershell
nuget restore AIMP-Discord-Presence-2.sln
dotnet build AIMP-Discord-Presence-2 -c Release -p:Platform=x86 -o publish\aimp_DiscordPresence2
powershell -File publish_plugin_zip.ps1 -Source publish\aimp_DiscordPresence2 -Output publish\aimp_DiscordPresence2.zip
```

`-p:Platform=x86` обязателен: AIMP — 32-битное приложение.

Готовая структура папки плагина:

```
Plugins\aimp_DiscordPresence2\
    aimp_DiscordPresence2.dll      # копия aimp_dotnet.dll, точка входа модуля для AIMP
    AIMP-Discord-Presence-2.dll    # сам плагин
    aimp_dotnet.dll                # мост AIMP DotNet
    AIMP.SDK.dll
    Newtonsoft.Json.dll
    DiscordRPC.dll
```

Если `aimp_DiscordPresence2.dll` нет, AIMP молча игнорирует весь плагин — он не появится
в списке. Подробности в [docs/Install.md](docs/Install.md).

## Известные ограничения

- Обложки нужны прямо в файле. В mp3 их почти всегда нет (либо заглушка 1×1) — в таком
  случае работает `embeddedInternetFallback`, а если обложку нужно гарантированно, зашейте
  её в файлы один раз через Mp3tag.
- Discord умеет показывать только картинку по публичному https-ссылке, поэтому «просто
  показать файл с диска» невозможно — обложку нужно куда-то загрузить. Провайдер Embedded
  заливает её на анонимный хост с автоудалением.

## Благодарности

- [iBowie](https://github.com/iBowie) — оригинальный AIMP Discord Presence 2.
- [martin211](https://github.com/martin211/aimp_dotnet) — AIMP DotNet SDK.
- [Discord](https://discord.com/developers/docs/rich-presence) — Discord Rich Presence.