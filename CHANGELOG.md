# Changelog

## [12.1.0.3]

- Обновлена страница настроек плагина: ссылка ведёт непосредственно на профиль API, где отображается API-KEY.
- Улучшены подписи поля API-KEY и кнопки сохранения.
- Обновлена пользовательская документация: установка, получение ключа, настройка загрузчика метаданных «КиноПоиск» в параметрах библиотеки.
- Исправлен процесс подготовки релизов: теперь публикация блокируется, если для версии отсутствует описание в CHANGELOG.md.


## [12.1.0.2]

- Исправлена регистрация metadata providers и remote image providers в DI-контейнере Jellyfin 12.
- Провайдеры КиноПоиска теперь доступны Jellyfin для поиска и загрузки метаданных фильмов, сериалов и персон.
- Исправлена интеграция с механизмом обнаружения провайдеров Jellyfin 12.

## [12.1.0.1]

- Исправлен прямой поиск фильмов по числовому ID КиноПоиска (например, 687670).
- Сохранена поддержка форматов `kp-687670` и `kp687670`.
- Исправлена обработка пустого ответа API при поиске.

## [12.1.0.0]

- Порт на .NET 10 и Jellyfin 12.1.
- Устранена генерация API-клиента из сети во время сборки.
- Ограничена параллельная проверка URL изображений, добавлены таймауты и отмена запросов.
- Добавлена пауза запросов при исчерпании квоты Kinopoisk API.
- Удалён встроенный общий API-токен; используется личный токен в настройках.

## [10.10.3.0]

Исправлена совместимость с 10.10.*

## [10.10.2.0]

Исправлена совместимость с 10.11.(0-4)

## [10.10.1.0]

Реализован IExternalUrlProvider (ссылки на кинопоиск)

## [10.9.8.0]

Перенесён значимый код из форка уважаемого @VD42

## [10.9.7.0]

Fixed pluginId

## [10.9.6.0]

Added YANDEX_DISK video source

## [10.9.5.0]

Fix staff to person kind matcher (by @bsv798)

## [10.9.0.0]

new release

## [10.8.9.3]

new release

## [10.8.9.2]

new release

## [10.8.9.1]

new release

## [10.7.5.6]

Update to .NET 7

## [10.7.5.4]

Search fixed in case of null premiere date

## [10.7.5.3]

Results parsing fixes due to upstream service changes (api 2.2)

## [10.7.5.2]

Results parsing fixes due to upstream service changes

## [10.7.5.1]

Results parsing fixes

## [10.7.5.0]

Short: Huge improvement in auto-identifying of movies. Rus: Значительно улучшено авто-привязка фильмов при добавлении в медиатеку. Пытаюсь найти идентификатор кинопоиска по паттерну kp-12345 или kp12345 в имени файла/папки. Пытаюсь найти единственное совпадение на кинопоиске по имени + году. Пытаюсь перебрать все совпадения по имени + сравнение IMDB ID.

## [10.7.0.5]

Added trailers filtering: jellyfin-web can only play youtube trailers, not kinopoisk-hosted

## [10.7.0.4]

Added person info, person image, trailers fetching

## [10.7.0.3]

Don't provide empty poster Added kinopoisk hyperlink in people info

## [10.7.0.2]

Fixed rating parsing

## [10.7.0.1]

Fixed some actor professions parsing

## [10.7.0.0]

Jellyfin v10.7 compat

## [10.6.0.2]

fix NRE in person provider

## [10.6.0.1]

new release

## [10.6.0.0]

new release
