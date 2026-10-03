name: Bug report / Ошибка

**Что произошло**
Опишите, что ожидалось и что получилось.

**Версии**
- AIMP (Help → About):
- Версия плагина (в списке плагинов AIMP):
- .NET Framework:

**Как воспроизвести**
1.
2.
3.

**Что уже проверяли**
- [ ] Обложка в файле есть и больше 100×100
- [ ] В `%AppData%\BowieD_AIMPDiscordPresence2\EmbeddedProvider\uploads.log` есть записи
- [ ] Endpoint из `config.xml` открывается из браузера
- [ ] В Discord включено «Show current session as a game presence»

**Логи**
Приложите содержимое `uploads.log`, если провайдер Embedded, и вывод
`dotnet build AIMP-Discord-Presence-2 -c Release -p:Platform=x86`, если проблема со сборкой.