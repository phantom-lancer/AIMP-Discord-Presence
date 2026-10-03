# MusicBrainz

Ищет обложку в интернете по названию альбома и артисту через MusicBrainz и
Cover Art Archive. Обложка в самом файле не нужна.

Настройки в `%AppData%\BowieD_AIMPDiscordPresence2\config.xml`:

| Параметр                 | Смысл                                                 |
| ------------------------ | ----------------------------------------------------- |
| `albumArtProvider`       | `MusicBrainz`, регистр важен                          |
| `musicBrainzUserAgent`   | User-Agent, обязателен — без него запросы отклоняются |

## Как включить

1. Полностью закрыть AIMP, включая трей.
2. Открыть `%AppData%\BowieD_AIMPDiscordPresence2\config.xml` любым текстовым редактором.
3. Провайдер: `<albumArtProvider>MusicBrainz</albumArtProvider>` — **регистр важен**.
4. User-Agent: заменить `<musicBrainzUserAgent />` на
   `<musicBrainzUserAgent>AIMPDiscordRPC/1.0.0 ( ВАШ@EMAIL )</musicBrainzUserAgent>`.
5. Запустить AIMP.

## Как это работает

По `Album` и `AlbumArtist` (или `Artist`, если альбом-артист пуст) находится release group,
затем его релиз на MusicBrainz, потом обложка берётся из Cover Art Archive: сперва
предпочтётся фронтальная, иначе первая доступная. Результат кэшируется до смены альбома.

## Ограничения

- MusicBrainz жёстко ограничивает запросы без нормального User-Agent и режет запросы без
  него совсем.
- Совпадение альбома не гарантировано: одноимённые альбомы, ремиксы и переиздания найдут не
  то. Для альбомов с одинаковым названием у разных исполнителей картинка будет одна.
- У провайдеров, которые режят MusicBrainz, провайдер не работает вовсе — в этом случае
  лучше [Embedded](Embedded.md).