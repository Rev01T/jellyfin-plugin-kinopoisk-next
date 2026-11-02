# КиноПоиск Next для Jellyfin 12

Плагин получает метаданные фильмов и сериалов из [КиноПоиска](https://www.kinopoisk.ru/) через Kinopoisk Unofficial API.

## Совместимость

- Jellyfin Server **12.1.x**
- .NET **10**
- Этот форк предназначен только для Jellyfin 12.x. Сборки для Jellyfin 10.x не поддерживаются.

## Установка

После публикации стабильного релиза добавьте репозиторий плагина в Jellyfin:

`https://raw.githubusercontent.com/Rev01T/jellyfin-plugin-kinopoisk-next/master/dist/manifest.json`

Затем откройте **Панель управления → Плагины → Каталог**, найдите «КиноПоиск» и установите плагин. Для ручной установки используйте ZIP из раздела Releases, соответствующий Jellyfin 12.

## Настройка API

Для работы нужен **личный API-токен** от [kinopoiskapiunofficial.tech](https://kinopoiskapiunofficial.tech/). После установки откройте **Панель управления → Плагины → Мои плагины → КиноПоиск → Настройки** и укажите токен.

В репозитории больше не задаётся общий токен: общий ключ быстро исчерпывает лимит запросов и не должен использоваться как общий ключ для всех установок.

## Возможности

Поддерживаются фильмы и сериалы. Плагин получает:
- рейтинг и описание;
- постеры и фоны;
- актёров и другие персоналии;
- трейлеры YouTube.

Для точного сопоставления укажите ID КиноПоиска в имени файла или папки: `kp-12345` или `kp12345`. Также используется поиск по названию, году выпуска и IMDb ID.

## Сборка из исходников

Требуется .NET 10 SDK.

```bash
dotnet restore ./src/Jellyfin.Plugin.Kinopoisk.sln
dotnet build -c Release ./src/Jellyfin.Plugin.Kinopoisk.sln
dotnet test -c Release ./src/Jellyfin.Plugin.Kinopoisk.sln
```

Артефакты сборки: `Jellyfin.Plugin.Kinopoisk.dll` и `KinopoiskUnofficialInfo.ApiClient.dll`.

## Разработка

Порт Jellyfin 12 ведётся в отдельной ветке. Перед публикацией релиза должны успешно завершиться сборка и тесты в GitHub Actions.
