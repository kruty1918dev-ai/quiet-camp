# Roadmap: план міграції та перевірок

Цей документ задає наступні етапи. У поточному аудиті runtime не змінювався. Будь-який art redesign починається після завершення baseline diagnosis; новий player build/installation — лише за окремим актуальним запитом користувача.

## Gate 0 — завершити native diagnosis до redesign

Джерело: [підтверджені findings і незакриті припущення](AUDIT-2026-10-07-UA.md). Control-flow defects доведені, фактичні frame-time причини на телефоні ще не атрибутовані.

За достатнього host headroom: окремий Editor test scene/save; один Unity run у capped slice, `-job-worker-count 1 -background-job-worker-count 4`. Не очищати чужі processes, `/tmp/qcb_*` чи device backups. Диск має мати ресурс для imports/results; зараз free <10%, новий clone/import не запускався.

Додати лише diagnostic markers/counters перед migration, без зовнішніх змін:

| Marker | Де | Що фіксувати |
|---|---|---|
| `Roadmap.Open` / `Catalog.Decode` | Configure / data loading | Cold/warm time, bytes, JSON calls, scene count |
| `Roadmap.GenerateScene` / `Story.Build` | Generator / library cache miss | Per-level ID/season, CPU, allocation stack, temporary native Mesh count |
| `Roadmap.Visibility` / `Pool.Rebind` | UpdatePool | Visits, retained/reassigned IDs, new/active/inactive slots |
| `Roadmap.Mesh.Background/Glade/Weather` | OnPopulateMesh paths | Time, vertex count, truncation, rebuild reason |
| `Roadmap.SnowSample` | Painter/SeasonProfile | Calls/prop/grid, corridor traversal, GC bytes |
| `Roadmap.Html.Refresh` / `Layout` | HtmlSurface / package host | Changed HTML, mount vs reconcile, Yoga passes, native allocations, ForceUpdateCanvases |
| `Roadmap.Preview` / `Navigate` | Bonus/Journey / Router | Root identity, creation/disposal, cancel tokens, scroll/readiness |

Збирати Timeline CPU, GC allocation call stacks, UI/Rendering module, mesh upload, batches/setpass, GPU time де підтримується, Frame Debugger для конкретних артефактів. Deep Profiling — лише окремий короткий діагностичний запис; його overhead не використовувати як performance результат. Memory snapshots cold → після першого traversal → після 10 loops/jumps → після виходу.

Сценарії актуального HEAD:

1. Main → Levels cold; повторити warm. Вхід одразу на current node і на кінець кампанії.
2. Idle 30 секунд у spring і winter, reduced motion on/off. Перевірити periodic rebuild spikes.
3. Повільний scroll через одну boundary; fling згори вниз/назад; п’ять швидких змін напрямку.
4. Levels → BonusPreview → back, 20 повторів; фіксувати native root IDs і scene Generate count.
5. Journeys → JourneyPreview → back; окремо від roadmap renderer, щоб не змішати full world build cost.
6. Levels → gameplay → Levels; Main Continue → gameplay → дія «до рівнів»; Album replay → gameplay → та сама дія.
7. Portrait/landscape/tablet/ultrawide; зміна viewport під час scroll, locale/text-scale refresh.
8. Inject missing model / generation failure / missing viewport в ізольованому fixture: мапа не публікує partial state, UI back залишається доступним.

Результат Gate 0: кожен відтворений фриз має timestamp, тривалість, marker/stack, phase (CPU/GPU/layout/GC), source revision; visual artifact — screenshot/frame-debugger event і точний scenario. Якщо причина не відтворилась, позначити unknown, не оголошувати виправленою. Після цього дозволений наступний implementation stage; aesthetic redesign усе ще окремий етап.

## Gate 1 — catalog/index/layout без зміни вигляду

- Indexed/versioned catalog з 30 existing summaries та light synthetic fixtures 300/1000.
- Один layout contract для art/UI; prefix bonus gaps; bounds table.
- Authoritative access adapter з parity для completion/bonus/Pro/Journeys.
- Existing canonical IDs, saves, snapshots та archive не переписуються.

Acceptance:

- Export/runtime validation відхиляє duplicate ID, wrong order/hash, empty/non-finite model, missing asset, неправильний bonus reference; atomic rollback до валідного каталогу.
- Координати всіх existing 30 nodes/3 draft bonuses збігаються зі старим backend у межах 0,5 UI unit.
- Main/bonus/story access відповідає старим правилам; виправлення Pro/UI mismatch тестується як явний change.
- 300/1000 synthetic fixture не потрапляє у shipped resources або user saves.
- Отримання одного node state не обходить увесь каталог. Editor не парсить campaign для кожної кнопки.

## Gate 2 — region window і lifetime

- Planner current±1; три owned leases, stable region ID; current ten-level grouping — кандидат, не догма.
- Cancellation/version token, staged commit, idempotent dispose, bounded buffer/control pools.
- Layout/geometry readiness barrier замість three-frame delay.

Acceptance:

- На кожному кадрі max active full regions ≤3, з правильним current і двома найближчими. Далекі регіони не мають active geometry/native controls/full descriptors.
- One-step boundary зберігає дві region leases, одна входить/одна виходить. Усередині pool перехід `[0,1,2]→[1,2,3]` зберігає 1/2, перебудовує тільки 3.
- 10 000 seeded planner jumps/forward/backward, 100 rapid selection/cancel cycles; stale result ніколи не стає visible.
- Fault під час preparation не змінює committed set; retry відновлює весь desired set. Missing viewport не активує усі N.
- Scroll/jump 300/1000: memory plateau після warmup залежить від трьох регіонів і shared assets, не від visited node count. Легкий catalog може рости з N.
- Виміряти найвищий viewport/zoom: або visible range вкладається у вікно, або documented coarse LOD/grouping. Немає прихованого четвертого full region.

## Gate 3 — UI virtualization та navigation

- Persistent HTML shell; visible+overscan controls, selective state patches.
- ID+localOffset anchor, typed return context.
- Bonus modal як sibling; один owned preview; roadmap subtree не remount при open/back.

Acceptance:

- 720×1600, 1080×1920, landscape 1920×1080, tablet 1280×800 і ultrawide 2560×1080; uk/en/de, 100%/130% text.
- Усі main/bonus вузли доступні scroll; нічого не опиняється поза hit area. Tap, drag-scroll, fling, Back, lock tooltip і preview працюють без double launch.
- Warm scroll не створює нових GameObjects, callbacks/listeners не дублюються. Native control count bounded viewport/overscan policy, не N.
- Levels ↔ BonusPreview 20 cycles: stable roadmap root ID, zero повторних whole-catalog Generate, anchor відновлюється.
- Disable/re-enable/configure binding зберігає поточний anchor і слухачі рівно один раз; відсутнє старе .6 при видимому .2.
- Locale/orientation refresh відновлює той самий node ID/offset; restart поводиться згідно documented persistence policy.
- Main Continue/Album/Story gameplay → дія «до рівнів» повертає правильну journey/map; completion оновлює вузли без stale lock.
- Transition reveal лише після готового viewport/anchor/visible meshes; немає first-frame flash верхньої мапи.

## Gate 4 — geometry/weather parity й оптимізація

- Cached static mesh/props/hulls, baked story geometry або bounded managed preparation.
- Winter grid sampling, heat data, seasonal surroundings parity.
- Analytic visible weather, dirty-reason invalidation, локальні bounds, explicit mesh split/LOD.

Acceptance:

- Existing 30 levels × Low/Balanced/High, усі сезони, clear/rain/snow/fog/night, tall trees/shore/fire; окремо overlap сусідніх chunks.
- Frame Debugger: правильний stencil/clipping/order; немає недомальованих meshes, z-order holes, pink/missing assets чи несподіваного UI-over-world overlap.
- Geometry budget задається одним policy: жодного silent truncation, counters zero; explicit split/LOD перевірений понад limit.
- Bounds містять усі projected triangles/shadows/weather/animation padding; після slow scroll немає поп-in на видимому краї.
- Winter root snow sample — один на prop; grid ≤289 unique samples до local exclusions. Fire melt збігається з source рівнем.
- Reduced/static idle не rebuild static glades щосекунди. Видимий wind/weather лишається адаптивним і не змінює gameplay.
- Warm ordinary scroll має 0 roadmap-owned GC allocations; допустимі сторонні UI/runtime allocations атрибутовані окремо, не приховані у загальному «0 GC».
- У cold path матеріали/моделі створюються один раз; 10 enter/leave loops не залишають owned Mesh/Material/GO поза lifecycle.

## Gate 5 — performance та rollout

Початкові **цільові**, не виміряні бюджети:

| Показник | Умова приймання |
|---|---|
| Roadmap main-thread work у warm scroll | p95 ≤2 мс, p99 ≤4 мс на обраному target device |
| Streaming preparation | Bounded slice ≤2 мс/frame; upload вимірюється окремо |
| Загальний CPU/GPU frame time | p95 ≤14 мс як запас під 60 FPS; frame p99 і thermal degradation також опублікувати |
| Довгі stalls | Немає roadmap-attributed stall >50 мс у warm scroll/bonus open; cold paths під cover виміряти окремо |
| Native region count | Завжди ≤3; controls/geometry не залежать від усіх N |
| Idle static work | Нуль structural mesh rebuild без reason; reduced motion не запускає animation invalidation |
| Memory | Plateau після warmup; немає монотонного зростання після 10 traversal/preview cycles |

Cold open не приховувати середнім FPS. Звіт містить median/p95/p99 open latency, max stall, JSON/generation/mesh/UI contributions; deadline loading UX встановити після baseline, не вигадувати цифру без пристрою.

Editor results не підтверджують телефон. Mobile build/install тільки після окремого дозволеного запиту; після нього — 20+ хвилин на середньому та слабкому пристроях, p95/p99, CPU/GPU, память, температура, input latency. SDK/API/renderer/device model/version фіксуються у звіті.

Legacy backend зберігати за feature flag до parity і performance pass. Перемикання backend не змінює save/content. Rollout gate — current 30 pass, synthetic 300/1000 bounded tests pass, native capture defects виправлені/класифіковані; rollback повертає старий renderer без міграції gameplay.

## Що не робити під час migration

Не додавати сотні повних `AlbumDiorama.BuildCamp`, не міняти правила головоломки, не додавати нові рівні, не переписувати album snapshots, не прикривати freeze постійним fade, не називати Editor FPS мобільним результатом. Art redesign, нова генеративна композиція та bonus content починаються окремо після цих gates.
