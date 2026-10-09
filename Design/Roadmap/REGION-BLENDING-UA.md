# Просторове змішування регіонів

Roadmap використовує один просторовий профіль для підкладки, 3D-ландшафту, галявин, рослинності, погоди, світла й звукового фону. Зміна region або chunk не перемикає тему кадру. Профілі визначаються положенням у світі; progress окремо обмежує доступний горизонт.

## Дані та authoring

У `RoadmapRegionData` поле `season` лишається канонічним: spring, summer, autumn, winter. Необов’язкове `environmentPhase` уточнює стан природи. Старі експорти без цього поля використовують `season`.

| Фаза | season | Особливість |
|---|---|---|
| spring | spring | Молоде листя, квіти, волога земля |
| summer | summer | Повна крона, комахи, тепле світло |
| dry-summer | summer | Сухіша трава, менше вологи |
| early-autumn | autumn | Перші золоті відтінки, майже повна крона |
| autumn | autumn | Золоте листя, менше трави |
| late-autumn | autumn | Оголені гілки, листя на землі |
| first-frost | winter | Невеликий сніговий покрив, переважно голі дерева |
| winter | winter | Сніг, без трави, зелені хвойні зі сніговими верхівками |
| thaw | spring | Частковий сніг, мокра земля, початок повернення рослин |

Приклад фрагмента регіону:

```json
{
  "season": "winter",
  "environmentPhase": "first-frost",
  "biome": "meadow",
  "lighting": "day",
  "weatherBias": 0.45,
  "transition": {
    "fromSeason": "autumn",
    "toSeason": "winter",
    "kind": "blend",
    "length": 1680
  }
}
```

`transition.length` — довжина зони в координатах roadmap, центрованої на межі регіонів. Sampler обмежує її 90% висоти меншого сусіднього регіону, щоб зони не перекривалися. Мінімальний бажаний span — 420; малі legacy-регіони обмежуються власною висотою. `kind: chapter` збережено для сумісності даних, але освітлення й природа також проходять через плавну зону. Валідатор відхиляє невідому фазу та її невідповідність сезону.

## Pipeline

`RoadmapCatalog → RoadmapEnvironmentSampler → RoadmapVisualProfile → renderer/scene/weather/audio`.

Sampler готує масиви профілів і меж один раз. Sample виконує binary search O(log regions), повертає immutable struct і не створює об’єктів. Smoothstep дає нульову швидкість зміни на обох кінцях зони. Змішуються палітри, листя, оголені гілки, сніг, вологість, туман, частинки, температура світла, рельєф та ваги видів рослин.

Native renderer фарбує вершини спільного terrain mesh за їхнім світовим положенням. Рельєф та густота декору також просторові. Вибір видів детермінований; це популяційне змішування, а не Instantiate/Destroy при кожному русі камери. Для голих дерев використовуються кешовані меші. Зменшення листя деформує крону, не стовбур. У зимовому профілі трава й квіти відсутні; сніг наростає й тане поступово. Периферійний водотік — легкий колірний мотив у тому самому меші, без reflection-camera; це не заміна gameplay water.

Projected renderer користується тими самими профілями та функцією освітлення для підкладки й галявин. Presentation switch чинної кампанії збережено. Native 3D залишається окремим opt-in каталогом під час міграції.

Погода й листя мають локальні бюджети; не додається ParticleSystem на кожний рівень. Reduced motion прибирає проліт листя. Sun/ambient/fog неперервно слідують положенню камери. Далекі невідомі сезонні деталі не розкриваються: camera sample обмежено progress horizon.

`RoadmapAmbientBlend` змішує наявні forest/meadow/autumn/winter beds, тиху воду й комах. Новий звук стартує з нульовою гучністю; старий згасає. Пул обмежений шістьма voice slots, включно з хвостами попереднього переходу. Menu soundscape плавно приглушується у roadmap. Звукові файли та gameplay audio не замінюються.

## Два приклади

`python3 tools/export_roadmap_transitions.py` створює presentation-only fixtures:

- `transition-summer-autumn`: 16 вузлів, summer forest → dry-summer meadow → early-autumn agricultural field → autumn forest.
- `transition-winter-thaw`: 20 вузлів, late-autumn forest → first-frost meadow → winter pines → thaw river → spring meadow.

Це графічні fixtures з синтетичними ID, не нова кампанія. Експортер не змінює puzzles, snapshots або progress. У чинному main authoring/export він лише збільшує довжину вже наявних transition zones; compiler використовує такі ж ширші зони для нових каталогів.

## Перевірки та обмеження

EditMode перевіряє колірну неперервність кожні 3 одиниці й безпосередньо по обидва боки кожної межі, монотонне накопичення/танення снігу, детермінізм після JSON round-trip, biome weights, validation і progress reveal.

PlayMode рендерить вхід/середину/вихід кожної зони, motion, portrait, tablet та приховане майбутнє. Перевіряє максимум три активні chunks, один native material і один RenderTexture. RAM-only saves не торкаються профілю гравця.

Ізольовані Editor виміри не є мобільним GPU/FPS тестом. AudioService вимкнений у графічних прогонах; реальне прослуховування переходів не підтверджене. Поточні frame-time, cold-open, unsupported counters та знімки наведено у [QA-звіті](../../TestResults/roadmap-blend-2026-10-07/REPORT_UA.md).
