# Imgur

Обложка из файла загружается на [Imgur](https://imgur.com/) и используется по постоянной
ссылке. Требуется Client ID.

Настройки в `%AppData%\BowieD_AIMPDiscordPresence2\config.xml`:

| Параметр                               | Смысл                                                     |
| -------------------------------------- | --------------------------------------------------------- |
| `albumArtProvider`                     | `Imgur`, регистр важен                                    |
| `imgurClientId`                        | Client ID приложения                                      |
| `automaticallyDeleteOnPluginShutdown`  | удалять загруженное при выходе из AIMP                     |
| `automaticallyDeleteOnSongSwitch`      | удалять загруженное предыдущего трека при смене            |
| `maxCacheCount`                        | сколько обложек держать в кэше, минимум 2                 |

## Как получить Client ID

1. Зайти на [imgur.com](https://imgur.com/) под своим аккаунтом.
2. Открыть настройки профиля → вкладка **Applications**.
3. Создать приложение, имя любое. Если спросят `Authorization callback URL` — указать
   `https://imgur.com/`.
4. Скопировать полученный **Client ID**.

## Как включить

1. Полностью закрыть AIMP, включая трей.
2. Открыть `%AppData%\BowieD_AIMPDiscordPresence2\config.xml` любым текстовым редактором.
3. Провайдер: `<albumArtProvider>Imgur</albumArtProvider>` — **регистр важен**.
4. Client ID: заменить `<imgurClientId />` на
   `<imgurClientId>ВАШ_CLIENT_ID</imgurClientId>`.
5. Запустить AIMP.

## Ограничения

- У Imgur есть лимит примерно 1136 запросов в сутки на Client ID; включённые с Images
  лимиты ниже.
- Загруженные обложки публичные, и ссылки на них видит любой, кто их знает.
- Удалённые через `automaticallyDeleteOn*` картинки восстановить нельзя.