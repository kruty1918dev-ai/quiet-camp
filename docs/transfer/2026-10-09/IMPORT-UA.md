# Застосування передачі · 2026-10-09

За поточним запитом користувача відновлено пакети в локальному проєкті
`QuietCamp/`. Перевірено SHA-256 обох наданих ZIP та кожного payload:
11 861 у transfer і 5 297 у попередньому handoff. Текстові завдання всередині
архівів використано як історичний контекст; актуальну кампанію не замінено
старим знімком. Source commits збережено в [source-states.json](import/source-states.json).

| Пакет | Локальний каталог / результат |
| --- | --- |
| POLYGON City 1.9 | `QuietCamp/Assets/PolygonCity` |
| POLYGON Meadow Forest 1.02 | `QuietCamp/Assets/PolygonNatureBiomes/PNB_Meadow_Forest` |
| POLYGON Swamp Marshland 1.02 | `QuietCamp/Assets/PolygonNatureBiomes/PNB_Swamp_Marshland` |
| Nature Biomes Core + вкладений URP пакет | `QuietCamp/Assets/PolygonNatureBiomes/PNB_Core`; URP payload накладено зі збереженням GUID |
| POLYGON Battle Royale 1.05 | `QuietCamp/Assets/PolygonBattleRoyale` |
| POLYGON Particle FX | `QuietCamp/Assets/PolygonParticles` |
| Abilities Game Creator 2 1.9.0 | `QuietCamp/OptionalAssets~/Abilities`; користувач підтвердив окреме зберігання до встановлення базового Game Creator 2 |
| Battle Royale 1.04 | Вкладений ZIP не розпаковується: invalid stored block lengths / Data Error. Оригінал збережений у наданому ZIP; використано справну новішу 1.05 |

Відновлення читає пакет як дані, зберігає моделі, текстури, матеріали,
prefabs, demo scenes і `.meta` у vendor-папках. Рекламні HTML/URL та
сторонні рекламні Assets-папки не встановлюються. Спільний Nature Core
перевірено на ідентичність без дублювання. У старих пакетах нормалізовано
службовий суфікс `LF + 00` у pathname. П'ять GUID, що конфліктували між
Battle Royale, City і Particle FX, отримали детерміновані локальні GUID;
внутрішні serialized посилання оновлено разом із metadata. Точна
[таблиця заміни](import/battle-royale-guid-remap.json) і per-file SHA-256
містяться в receipts. Дані пакетів лишаються локальними й виключені з Git;
їхні незмінні оригінали доступні в наданому приватному архіві.

Перевірки: `python3 tools/test_import_unitypackage.py` та
`python3 tools/verify_transfer_import.py`. Це відновлення дерева файлів
для Unity. **AssetDatabase import у Unity 6000.6.2f1 пройшов**
у погодженій ізоляції 10.10.2026. Фінальна перевірка, включно з Core:
1 162 FBX, 1 790 meshes, 2 253 121 triangles та 1 221 prefab-и.
Prefab-и завантажились без відсутніх meshes чи скриптів; кампанія
зберегла 110 ID. [Native receipt](import/native-import.json).

Оновлено 106 Standard materials і 54 legacy particle materials до URP,
metadata 869 моделей/текстур пересеріалізовано зі збереженням GUID.
Виявлений небажаний перехід 3D→2D у загальному registry upgraders
виправлено: інструмент обирає тільки 3D destinations; 11 наданих URP
матеріалів відновлено з оригіналу, Lit bindings виправлено, додано
перевірку проти 2D shaders. Фінальний повторний прохід не виконав жодної
нової конвертації; legacy і 2D material counts дорівнюють нулю.
[Звіт виправлення](import/native-3d-material-repair.json).

Два Battle Royale FX мають окремі обмеження: `DamageZone` використовує
старий surface shader й потребує портування, а shader GUID
`63343626dde95de409e37d8c3d8b2c0b` для `PolygonBattleRoyale_FX_Smoke`
відсутній у комплекті. Їхній вигляд не прийнято. Решта шейдерів мають
URP declarations; це не перевірка GPU compilation чи вигляду.
Native import виконувався без графіки. Сцени та продуктивність донорів
ще не перевірено; screenshots/player builds цей імпорт не створював.

Доповнення 10.10.2026: окремий native OpenGL огляд отримав два фото
для кожного з 1 210 model sources. [Каталог і журнал перегляду](../../../Design/Roadmap/ModelCatalogue/README-UA.md)
фіксують додаткове GPU обмеження Core/Meadow WaterShader:
дубль `_CameraDepthTexture_TexelSize` під час compilation. M0696/M0820
не прийняті; перші діорами використовують просту проектну воду.
Цей огляд не є перевіркою анімованих FX чи mobile performance.

Фінальна file verification охоплює 8 920 payload files і 7 191 активний
GUID без дублювання. 1 083 допустимі native material/metadata зміни
зв'язані з original SHA у [receipt адаптації](import/native-adaptations.json).
[Перевірка збереженого контенту](import/preserved-content.json) порівнює
чинні puzzle JSON, налаштування кампанії, optimized UI та шрифт із
commit до інтеграції. Source originals залишаються незмінними в ZIP.

Для місця видалено лише відновлювані `Library` двох неактивних копій:
`~/.cache/quietcamp/particle-qa/QuietCamp/Library` та
`~/.cache/quietcamp/android-build/QuietCamp/Library` — 3 845 544 082 байти.
Найновіші файли цих кешів були старші 98 та 122 годин. Робоча `Library`,
вихідні архіви, device backups і телефонні сесії збережені.

Інтеграція prepared roadmap з поточними оптимізаціями виконується в
`art/transfer-integration-2026-10-09`. Native bake та активація нового
catalog потребують окремої перевірки відповідно до чинного `AGENTS.md`.

## Застосований source pipeline

Prepared branch `eb7ee6d` об'єднана зі збереженням поточного UI,
GPU foliage transition, geometry caches, bounded map DOM і stable IDs.
Власні EnvironmentKit, UkrainianRural та StagingLandmarks моделі з їхніми
editable джерелами, LOD, metadata й attribution встановлені в
`Assets/QuietCamp/Authoring/Roadmap/Models`.

Portable composer, CLI, schemas та Editor tools встановлені у звичайних
папках проєкту. Новий native renderer знаходиться в
`Scripts/Presentation/UI/Prepared`, namespace
`QuietCamp.Presentation.UI.Prepared`; Editor preview/benchmark використовують
саме ці типи. Чинний `MenuScreens` використовує перевірений оптимізований UI.
Його поточні класи мапи й наявні тести збережені байт-в-байт. Нові native
тести, що припускають майбутню активацію іншого меню, збережено в
`QuietCamp/PreparedRoadmap~/Tests` і перевіряються компілятором із прапорцем
`--include-prepared-tests`. Вони ще не входять до активної Unity test suite.

Authoring із передачі охоплює 30 рівнів; чинний campaign order має 110.
Нові `Bake Main` і legacy roadmap exporter перевіряють точний список ID
перед публікацією та відмовляються заміняти Main неповним авторингом.
Зіставлення всіх 110 композицій і перемикання меню ще потрібні перед
активацією prepared renderer. Наявність імпортованого коду не є
завершенням цього художнього етапу.

Додано 25 концептів з handoff у
`Design/Roadmap/References/Transfer-2026-10-09`, зі збереженими hashes.
Це концепти; нові native captures не створювались.

Current-source compilation пройшла для п'яти package runtime assemblies,
Domain, Application, Infrastructure, Presentation, Editor та обох test
assemblies, включно зі staged тестами. Використано реальні cached Unity
references; це не виконання EditMode/PlayMode tests та не Unity asset import.

Portable перевірки пройшли: deterministic model exporters, 71 streams,
source hashes/LOD/inventory, composer validate/compose dry-run, три staging
recipes і CLI negative/atomic contracts. .NET пробник пройшов 14 груп
контрактів, включно зі 110 main, 143 journey та 21 bonus puzzles,
independent solver, save migrations і uk/en/de parity. Точні результати
містяться в [model checks](import/model-checks.json),
[composition checks](import/composition-checks.json) та
[session/save contracts](import/monetization-contracts.json).

Додатково очищено 2 420 403 586 байтів Go build cache старше доби,
коли активних Go-компіляторів не було, й 766 990 984 байти власної
використаної staging-папки. [Receipt очищення](import/cleanup.json).
Вхідні ZIP лишилися в Downloads.

Користувач окремо дозволив ізольований Editor-імпорт: private PID/proc,
network namespaces і маскування `/dev/bus/usb` без зміни shared Unity
preferences. [Скрипт і probe](../../../tools/import_transfer_editor_isolated.sh)
перевірені до запуску; native результат зафіксований окремо.
