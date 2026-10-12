> Найновіші кадри й покадровий розбір дев’яти циклів: [RiverReview](RiverReview/README-UA.md). [Reviewed](Reviewed/README-UA.md) і результати нижче показують попередні версії генератора.

# Генератор композиції та native докази — 12 жовтня 2026

[Методологія, правила та взаємодія систем](METHODOLOGY-UA.md).
Розміщення тепер виконується автоматичним C# pipeline трьох повторно придатних
пакетів. Запит QC007 містить тільки профіль і seed; списку координат сцени немає.

## Рендери

| Кадр | Що він доводить |
|---|---|
| [QC007 Gameplay, 1536×1024](Renders/QC007-generated-gameplay.png) | Поточна згенерована композиція у справжній сцені Camp після переходу з меню |
| [QC007 Gameplay, 1920×1080](Renders/QC007-generated-gameplay-wide.png) | Широкий кадр цієї самої сцени |
| [QC007 preview](Renders/QC007-generated-landscape.png) | Native рендер production world components, профіль riparian-clearing, seed 12007 |
| [QC007 portrait, 1080×1920](Renders/QC007-generated-portrait.png) | Те саме розміщення; покриття світу без оголених кінців водойми й terrain |
| [QC001, seed 41001](Renders/QC001-generated-landscape.png) | Інше поле та seed, той самий профіль, без ручного розташування |
| [QC009, seed 41009](Renders/QC009-generated-landscape.png) | Ще один gameplay набір, той самий профіль |

Кожна PNG має сусідній JSON receipt з hashes зображення, resolved snapshot,
джерел, level content hash, camera, seed і профілю. Це Unity/DX12 рендери,
не AI-генерація і не зовнішнє редагування PNG. Gameplay кадри приховують HUD
та службові overlays для огляду сцени; witness ставиться тільки у renderer.

Захоплення зроблено в уже відкритій копії `C:/Users/Public/qcui-20261006/QuietCamp`.
Її рівні історично відрізняються від 100-level baseline у GitHub checkout.
Наприклад, активний QC007 має content hash `7d54113d6df37600c093ababc533f3a4fa0af4fc80d2217232f903d23d718eca`,
а canonical QC007 — `24f155ac8a3082e20fc343c92711f147141710a9e0519544c7714bcad3b03970`.
Обидва поля 6×8. Пазли не синхронізувалися й не переписувалися; сам генератор,
профіль та геометрія застосовані до обох копій. Ці PNG не слід представляти
як native capture іншого content hash.

## Перевірка

- 1083 .NET Core assertions пройшли: layout/contact/framing, ecology,
  groundcover, rock groups та попередні контракти пакетів.
- 20 справжніх Unity EditMode tests пройшли, 0 failures, 0 skipped.
- Runtime, Editor і test sources canonical checkout скомпільовані проти
  актуального Unity cache; окремо пройшла player-conditioned source compilation.
- Меню → QC007 → Camp.UiReady перевірено у Play Mode. QA saves після виходу
  відновлено; renderer asset залишено без змін.
- Player build не виконувався; мобільна продуктивність не виміряна.

Повторити Core contracts: `python tools/verify_scene_generation.py`.
Native helper: `python tools/capture_scene_generation.py --help`.

## Критична оцінка

Під час ітерацій виправлено надто великі листяні деталі, очерет у проході,
нечитабельні квіткові faces, неправильне покриття portrait viewport і другий
legacy canopy spawner, який перекривав намет. Generated surround тепер має
одного власника дерев; логічні shade masks пазла та UI підказки збережено.
Фізичні scenic shadows не є джерелом рішення пазла.

Точне повторення **AI-референсу** не досягнуто. Найбільші відмінності —
геометрія крон, деталізація берега/води й характер освітлення. Це придатна
перевірена основа генератора композицій, а не доказ автоматичного художнього
приймання або піксельної тотожності. Приймання в іншій грі потребує її asset
adapter, власного профілю та native перевірки.

Пакетні ревізії: scene-placement `a13bac3`, vegetation-placement `576a254`,
rock-placement `2b34673`. Core sources цих ревізій вбудовані в QuietCamp.
