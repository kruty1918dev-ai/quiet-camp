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
для Unity, **не виконаний AssetDatabase import**. Editor не запускався;
native shader conversion, сцени, вигляд і продуктивність цих донорів ще
не перевірено. Свіжі screenshots не створювалися, player builds не було.

Для місця видалено лише відновлювані `Library` двох неактивних копій:
`~/.cache/quietcamp/particle-qa/QuietCamp/Library` та
`~/.cache/quietcamp/android-build/QuietCamp/Library` — 3 845 544 082 байти.
Найновіші файли цих кешів були старші 98 та 122 годин. Робоча `Library`,
вихідні архіви, device backups і телефонні сесії збережені.

Інтеграція prepared roadmap з поточними оптимізаціями виконується в
`art/transfer-integration-2026-10-09`. Native bake та активація нового
catalog потребують окремої перевірки відповідно до чинного `AGENTS.md`.
