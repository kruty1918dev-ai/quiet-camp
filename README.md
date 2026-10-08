<p align="center">
  <img src="Design/AppIcon/quietcamp-google-play-512.png" width="104" alt="Quiet Camp — намет і тепле багаття" />
</p>

<h1 align="center">Quiet Camp · Тихий кемпінг</h1>

<p align="center"><strong>Знайди місце для кожного гостя. Збережи табір, до якого хочеться повернутись.</strong><br />
Спокійна просторова головоломка у власному low poly світі — з лісом, чотирма сезонами й маленькими спогадами.</p>

<p align="center">Unity 6 / URP · Offline-first · Українська / English / Deutsch · Android development project</p>

<p align="center">
  <a href="https://kruty1918dev-ai.github.io/quiet-camp/">Сайт гри</a> ·
  <a href="docs/README.md">Довідник</a> ·
  <a href="docs/gameplay.md">Як грати</a> ·
  <a href="docs/screens.md">Меню й панелі</a> ·
  <a href="docs/world.md">Світ</a> ·
  <a href="docs/gallery.md">Галерея</a> ·
  <a href="docs/development.md">Відкрити проєкт</a> ·
  <a href="docs/project-map.md">Карта коду</a>
</p>

![Літня галявина Quiet Camp — справжній Unity Game View](docs/images/captures/2026-10-08/album-season-summer.png)

*Кадри довідника: Unity Game View, 2026-10-08, окремий QA-профіль із synthetic progress. [Середовище, source hashes і статус перевірок](docs/status.md).*

## Місце для кожного

Гості хочуть різного: проходу до дверей, тіні, тихого куточка або друга поруч. Обери намет, поверни його й знайди місце на галявині. Маленькі рішення складаються в цілий табір — із деревами, водою, вогнем і звуками природи. Таймера завершення рівня немає.

Завершений табір лишається у **Моїх кемпінгах**. Його можна роздивитися, обернути, повторити puzzle або прибрати основні панелі й просто залишитись на мить. Світ змінюється із сезонами; decorative weather зберігає правила puzzle.

**Quiet Camp** is a calm, offline-first campsite puzzle. Arrange tents around paths, shade, quiet and friendship, then revisit your saved seasonal 3D camps. This repository contains the development project and its illustrated guide.

## Подивись, перш ніж читати код

| Головне меню | Мапа місць | Твій кемпінг |
| --- | --- | --- |
| <a href="docs/screens.md#головне-меню"><img src="docs/images/captures/2026-10-08/01-main-menu.png" width="270" alt="Головне меню й Остап" /></a> | <a href="docs/screens.md#мапа-галявин"><img src="docs/images/captures/2026-10-08/04-roadmap.png" width="270" alt="Мапа з вузлами прогресу" /></a> | <a href="docs/screens.md#мої-табори"><img src="docs/images/captures/2026-10-08/15-album.png" width="270" alt="Збережений 3D-кемпінг у альбомі" /></a> |

Кожен екран має власне пояснення в [атласі меню й панелей](docs/screens.md): як відкрити, що робить кожна область і де знайти реалізацію. [Галерея](docs/gallery.md) містить повні PNG та JSON sidecars.

## Чотири прості побажання

![Пояснювальні схеми правил: шлях, тінь, тиша, друзі](docs/images/diagrams/rules.svg)

| Шлях | Тінь | Тиша | Друзі |
| --- | --- | --- | --- |
| Двері доступні мережею проходів. | Увесь footprint намету в заданій тіні. | Намет поза зоною шуму багаття. | Між дверима друзів до трьох кроків проходом. |

Drag, поворот, undo/redo, список гостей і placement preview допомагають скласти розташування. Перевірка зʼявляється, коли всі намети на місці. [Правила, керування й ресурсна модель →](docs/gameplay.md)

## Світ, до якого повертаєшся

| Весняна галявина | Осінній табір |
| --- | --- |
| ![Весна в Unity Game View](docs/images/captures/2026-10-08/album-season-spring.png) | ![Осінь у Unity Game View](docs/images/captures/2026-10-08/album-season-autumn.png) |

Поточний main content catalog задає **110 місць головної дороги**, 15 districts і окремі journey branches. Це кількість у source catalog; visual/puzzle/device acceptance має власний перевірений обсяг. [Світ, зима, подорожі та staging →](docs/world.md)

Навчання з Остапом, три мови, text scale, contrast, reduced motion, calm pacing, audio/haptics і portrait/landscape settings дають грати у своєму темпі. [Налаштування з ілюстраціями →](docs/screens.md#налаштування)

## Знайди потрібне в репозиторії

| Для кого | Почати тут |
| --- | --- |
| Хочу зрозуміти гру | [Ілюстрований довідник](docs/README.md), [правила](docs/gameplay.md), [екрани](docs/screens.md). |
| Хочу подивитися весь інтерфейс | [Галерея](docs/gallery.md), [атлас меню](docs/screens.md). |
| Відкриваю Unity вперше | [Перший запуск і залежності](docs/development.md#перший-запуск). |
| Шукаю файл/систему | [Карта проєкту](docs/project-map.md), [scripts](QuietCamp/Assets/QuietCamp/Scripts), [resources](QuietCamp/Assets/QuietCamp/Resources/QuietCamp). |
| Працюю над art/story/UI | [Design index](Design/README.md), [technical documentation](Documentation/README.md), [campaign roadmap](CAMPAIGN_ROADMAP.md). |
| Потрібні докази готовності | [Стан і перевірки](docs/status.md), [capture workflow](tools/qa/DOCS-CAPTURE-UA.md), [workspace rules](AGENTS.md). |

![Як головні екрани повʼязані між собою](docs/images/diagrams/screens-flow.svg)

## Відкрити локально

1. Використовуй **Unity 6000.6.2f1**, revision `770e33f6875c`.
2. З кореня repo виконай `python3 tools/verify_portability.py` (Windows: `py -3 tools/verify_portability.py`).
3. Відкрий у Unity Hub папку **QuietCamp/**, дочекайся package resolution/import.
4. Відкрий `Assets/QuietCamp/Scenes/Boot.unity` і використовуй Editor Play Mode у дозволеній сесії.

Custom local UPM packages включені в [Packages](QuietCamp/Packages/README.md); сусідні repositories і копія старої Library не потрібні. Registry/Git packages мають exact pins і потребують internet для першого resolve. Для .NET probe потрібен SDK 10. [Повна інструкція, architecture й checks →](docs/development.md)

## Поточний статус

**Проєкт у розробці; store release не заявляється.** Main має campsite gameplay, scene routing, локальні saves, навчання, UI й album. Default configuration offline-first, без обовʼязкового account/cloud save та enabled advertising/analytics providers.

Життя, підказки, currency exchanges, Pro і purchase-state contracts — prototype integrations. Новий профіль починає з трьох hints і пʼяти lives; incorrect Check витрачає життя й очищає arrangement/history. Реальні покупки та rewarded ads не підключені. [Ресурси й Pro](docs/screens.md#ресурси-та-pro).

Нова forest/village/power/aircraft/dam roadmap composition підготовлена в [окремій art branch](https://github.com/kruty1918dev-ai/quiet-camp/tree/art/roadmap-native-finish-2026-10-08). Її не злито в main і не активовано новим native bake; кадри довідника показують main renderer. Device performance, store/restore/ad acceptance та staging publication мають окремі наступні checks. [Точний стан доказів →](docs/status.md)

Ліцензії art/audio/vendor sources зберігають власні умови та attribution; [asset notes](docs/development.md#assets-і-ліцензії). Концепти й store mockups позначені окремо в [архіві галереї](docs/gallery.md#архів-та-концепти).
