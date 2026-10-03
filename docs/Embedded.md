# Embedded: обложка из файла трека

Провайдер по умолчанию. Берёт обложку, **встроенную в сам аудиофайл**, ужимает её и
загружает на анонимный хостинг, потому что Discord Rich Presence умеет показывать только
картинку по публичному https-ссылке. Локальный путь или `file://` не работают, какой бы
строкой их ни подставляли в `LargeImageKey`.

## Как это работает

1. Берётся `IAimpFileInfo.AlbumArt` — обложка из тегов файла (`APIC` в mp3, `PICTURE` в flac,
   `covr` в m4a).
2. Если картинка меньше `embeddedMinDimension` по обеим сторонам — она считается заглушкой,
   обложки нет.
3. Обложка масштабируется до `embeddedMaxDimension` и кодируется в JPEG.
4. Загружается на `embeddedUploadEndpoint` (по умолчанию
   [litterbox](https://litterbox.catbox.moe) — без ключа, без аккаунта, без регистрации).
5. Полученный https-ссылка отдаётся в Discord. Результат кэшируется по имени и размеру
   файла, так что при переключении треков обратно обложка не заливается заново.

## Настройки

Всё в `%AppData%\BowieD_AIMPDiscordPresence2\config.xml`:

| Параметр                   | По умолчанию                                          | Смысл                                                     |
| -------------------------- | ------------------------------------------------------ | --------------------------------------------------------- |
| `albumArtProvider`         | `Embedded`                                             | выбор этого провайдера                                    |
| `embeddedUploadEndpoint`   | `https://litterbox.catbox.moe/resources/internals/api.php` | endpoint загрузки (litterbox-совместимый)                |
| `embeddedUploadExpiry`     | `1h`                                                   | время жизни файла: `1h`, `12h`, `24h`, `48h`, `72h`        |
| `embeddedMaxDimension`     | `512`                                                  | больше этого обложка ужимается                             |
| `embeddedMinDimension`     | `100`                                                  | меньше этого — заглушка, обложка не ищется                 |
| `embeddedInternetFallback` | `true`                                                 | нет обложки в файле — искать в MusicBrainz                  |
| `embeddedFallbackUserAgent`| `AIMP-Discord-Presence-2/0.0.3`                        | User-Agent для запроса к MusicBrainz                        |
| `coverCacheEnabled`       | `true`                                                 | помнить обложки между запусками AIMP                        |
| `coverPrefetchEnabled`    | `true`                                                 | заранее грузить обложку следующего трека                    |

Лог каждой загрузки: `%AppData%\BowieD_AIMPDiscordPresence2\EmbeddedProvider\uploads.log`.
Большой лог — признак того, что endpoint недоступен из вашей сети.
Лог MusicBrainz-фолбэка: `%AppData%\BowieD_AIMPDiscordPresence2\MusicBrainzProvider\musicbrainz.log`.

## Ничего не тормозит

Провайдер никогда не блокирует вызывающий код:

- `TryGetImageUrl` возвращает только то, что уже готово, а отсутствие обложки запускает
  загрузку в фоне и возвращает пустую строку;
- таймаут загрузки — 15 секунд, таймаут MusicBrainz — 6 секунд;
- после трёх неудач подряд MusicBrainz отключается до конца сессии, чтобы не висеть на
  каждом треке;
- трек с неудачной загрузкой не пробуется снова 10 минут;
- обложки сохраняются в `EmbeddedProvider\cache.json` вместе со сроком жизни ссылки, поэтому
  после перезапуска AIMP они не загружаются заново, а протухшая ссылка заменяется сама.

## Обложки в mp3 обычно отсутствуют

В дикой природе mp3 почти всегда без обложек: либо кадра `APIC` нет, либо внутри заглушка
1×1. Такие обложки отсекаются по `embeddedMinDimension`, и дальше:

- если `embeddedInternetFallback` = `true` (по умолчанию), обложка ищется в MusicBrainz и
  используется напрямую, без загрузки;
- если нужны строго локальные обложки — поставьте `false` и зашейте картинки в файлы один
  раз. Например, Mp3tag: выделить треки → «Tag pictures» → указать папку с обложками.
  В flac обложки обычно уже есть, и тогда всё работает без всякого фолбэка.

## Альтернативный endpoint

`embeddedUploadEndpoint` должен принимать multipart POST с полями `reqtype=fileupload`,
`time=<embeddedUploadExpiry>`, `fileToUpload=<файл>` и отдавать ссылку текстом или JSON с
полем `url`/`link`. Такую же схему использует [catbox](https://catbox.moe), поэтому, если
litterbox с вашей сети недоступен, можно указать адрес catbox: `https://catbox.moe/user/api.php`.

Если MusicBrainz недоступен (бывает у части провайдеров), откат не сработает — это видно по
пустому логу и дефолтной иконке AIMP.