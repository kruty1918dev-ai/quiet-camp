# Quiet Camp — кінематографічна долина та структура кампанії

> Рельєф і світло 10.10: схили перенесено у видиму частину долини, додано направлене світло неба й дальню імлу. [Native до / після](Design/Roadmap/DepthReview/2026-10-10/index.html), 43 кадри 19:10:30 UTC; Game View нової ревізії ще перевіряється.

> Навігація: [ілюстрований довідник](docs/README.md) · [світ і source catalog](docs/world.md) · [галерея](docs/gallery.md) · [стан перевірок](docs/status.md).
>
> Тіні 10.10: усі непрозорі об’єкти та рослинність відкидають тіні від одного сонця; об’єднання однакових вершин скоротило їхню кількість на 22,7–22,9% без зміни силуетів. 43 native кадри й новий Game View flow Passed, відео оновлено. [Бюджет тіней](Design/Roadmap/CinematicPilot/2026-10-10/shadow-budget-receipt.json): atlas 512/1024, один каскад; Editor evidence не означає mobile FPS.

> Читабельність дороги 10.10: зупинка повернута до двосмугового асфальту; owned bay/platform і roadside waystone готові. Порівняння Low/Balanced, portrait/landscape, 43 native кадри та свіжий Game View flow Passed; відео оновлено. Покриття повторює фактичні трикутники землі, дальній рельєф не перекриває дорогу. [Дорога](Design/Roadmap/StopRoad/2026-10-10/index.html).

> Художній прохід 10.10: відібрано дев’ять виразніших наявних моделей, native кадри та бюджети перевірено. Рослинність має повні контури з запасом для вітру, берег і споруди виключені з посадок; безперервну дорогу й рельєф оновлено. 43 native кадри та незалежна перевірка відступів Passed; поточний Game View flow, тіні та рух води Passed, відео оновлено. [Добір моделей](Design/Roadmap/ModelRefinement/2026-10-10/README-UA.md).

> Поточна заміна роадмапи: [QC001–QC005 в українській долині](Design/Roadmap/CinematicPilot/2026-10-10/README.md). П’ять місць і чотири переходи пройшли native render review; повний Game View integration Passed. Камера віддалена на 45% для огляду околиць; керування прив’язане до точки торкання, без нових елементів UI. Дані full main catalog зберігають 110 ordered places; цей документ описує релізний план ширшого світу. Усі гілки об’єднано в main зі збереженням їхніх commits. Кількість у source catalog не є заявою native/device acceptance всього контенту. Нова semantic roadmap composition вже є в main як авторинг; чинний runtime використовує окремий п’ятиточковий native world index.
>
> Native Editor performance, 2026-10-09: перший раунд оптимізації виконано й переміряно — leaf CPU p95 17.34 → 0.03 мс (GPU shader), найдовші route кадри 960 → 237 мс, map open max 1 377 → 303 мс. **14 EditMode / 13 PlayMode regressions Passed**; budgets пройдені частково, залишкова черга в [карті продуктивності](PERFORMANCE_MAP.md) · [інтерактивний report](https://kruty1918dev-ai.github.io/quiet-camp/performance.html).


Ціль: ~300 рівнів, що відчуваються як жива карта світу з центральною
сюжетною дорогою та відгалуженнями — а не лінійні 1–300.

Дизайн-етап 10.10.2026: [12 українських діорам](Design/Roadmap/DioramaStudies/2026-10-10/index.html)
для першого вибору стилю; [план композицій та відповідність районам](Design/Roadmap/DIORAMA-DESIGN-UA.md).
Каталог охоплює 1 210 model sources і 2 420 native фото, переглянутих на
61 аркуші. До діорам увійшли 16 нових donor sources та власний український
набір; source bindings містять ще один резервний знак. Це попередній дизайн-етап; його пазли й 110 IDs збережені в даних.
Подальша затверджена заміна runtime — безперервна долина QC001–QC005
з дальньою камерою та прямим керуванням, описана на початку документа.

## Загальна структура

- **Головна дорога (110 рівнів)** — `QC001–QC010` + `gen:qc_camp:1–32`
  + `gen:qc_camp:55–122`. П'ять актів; 15 районів по 3–8 рівнів.
- **Сюжетні гілки (143 рівні)** — journeys: маяк (staging), стара
  станція, теплиця, гірський маршрут, покинутий курорт, узбережжя +
  безкоштовні memories. Відкриваються прогресією / жаринками /
  покупкою; ніколи не перебудовують основну стежку.
- **Bonus/special (21 слот)** — приховані галявини, сезонні місця,
  challenge- і premium-слоти вздовж усієї дороги (кожні 5 рівнів).

## Головна дорога: акти й райони

| Акт | Рівні | Райони | Арка |
|-----|-------|--------|------|
| 1 | 1–30 | glades, forest, embers | Навчаємося створювати безпечні місця |
| 2 | 31–58 | bridges, stations, shores, farms, mills | Місця починають з'єднуватися |
| 3 | 59–82 | villages, passes, resort | Люди повертаються; гори й руїни курорту |
| 4 | 83–106 | coast, highlands, frontier | Витримані місця: берег, високогір'я, кордон |
| 5 | 107–110 | haven | Мережа місць завершена — світ живий |

## Історичний ритм повної кампанії

- Історичний задум story cards замінено для нового пілота історією безпосередньо в оточенні.
  (`journey.main.story.N`, у `MonetizationCatalog` — біти призначаються
  на кожен 5-й рівень + на кінець кожного району; 31–42 лишаються
  щільним блоком переходу).
- **Кожні 10–15 рівнів** — новий район: одноразове інтро Остапа
  (`district.*.intro`), нові мотиви оточення від генератора.
- **Кожні 25–35 рівнів** — межа акту = завершення арки (останній район
  акту + story beat на межі).

## Сюжетні гілки

| Гілка | Рівнів | Відмикач |
|-------|--------|----------|
| memories | 3 (`gen:qc_camp:33–35`) | 15 місць головної дороги |
| garden | 28 (`gen:qc_gd:1–28`) | 30 місць + 300 жаринок |
| station | 18 (`gen:qc_st:1–18`) | 45 місць + 300 жаринок |
| lighthouse | 20 (`QC_LH001–008` + `gen:qc_lh:9–20`) | staging: unpublished, 55 місць |
| mountain | 24 (`gen:qc_mt:1–24`) | 65 місць + 500 жаринок |
| resort | 30 (`gen:qc_rs:1–30`) | 80 місць + 500 жаринок |
| coast | 20 (`gen:qc_cs:1–20`) | 90 місць + 400 жаринок |

Гілки повертають тих самих персонажів у нових обставинах і показують
наслідки рішень головної дороги (story keys per-level у journeys).

## Bonus/special слоти

Слоти в `bonus_camps.json` на позиціях кожні ~5 рівнів: стандартні
галявини, сезонні вікна (`seasonId`), premium- і challenge-варіанти.
Гейтинг: `afterLevel` + `requiredCompletions` (останній десяток рівнів)
+ `requiresPremium` + сезон. Масштабується до ~100 слотів додаванням
записів — рівні випікаються `gen:qc_bn:*`.

## Дані й пайплайн

- `tools/campaign-authoring/Program.cs` — детермінований план
  (id, pacing-number) для всіх сімей; валідатор + witness + солвер.
- `tools/regenerate_campaign.py` — frozen JSON + хеші + ContentRevisions
  + базові LegacyLevels для нових id + .meta.
- `campaign.json`: `generatedLevelIds` = порядок головної дороги,
  `districts` = райони/акти, `contentVersion` = cozy-campaign-4.
- Зона зайнятих `gen:qc_camp` id: 1–32 (main 11–42), 33–35 (memories),
  51–54 (bonus), 55–122 (main 43–110). Сім'ї гілок: qc_gd, qc_st,
  qc_lh, qc_mt, qc_rs, qc_cs; бонуси: qc_bn (21 випечений: 17 слотів
  активні, 4 у резерві).

## Стан реалізації

- Випечено й заморожено 270 рівнів: 110 main + 143 journey + 21 bonus —
  усі пройшли валідатор, збережений witness і незалежний солвер.
- Опубліковані раніше id не змінилися байт-в-байт; нові рівні отримали
  salted seeds (`IdSalt`), LegacyLevels-базові архіви та .meta.
- Мапа: центральна дорога + bonus-вузли кожні 5 рівнів + branch-вузли
  (`map-branch-stop`), що відкривають JourneyPreview; райони з act-CSS.
- Гілки без замороженого контенту автоматично unpublished
  (`MonetizationCatalog.Catalog` + `LevelLoader.Exists`).
- Локалізація uk/en/de: 501 ключ у кожній (district, journey.*,
  map.bonus.*, story beats); паритет перевіряється пробником.

## Поза межами

- Платні гілки лишаються staging: покупки вимкнені, entitlement —
  метадані для майбутньої активації.
- Rewarded-ads як відмикач — зарезервовано (немає інфраструктури реклами).
- Свіжий контент за оновленнями: сезонні вікна дадуть нові слоти без
  зміни коду.

## Checkout portability — 2026-10-08

Bundled AgentVerify, Atmos, LevelGen and LevelKit with recorded upstream revisions and original metadata/licenses. The manifest no longer requires four sibling repositories. `tools/verify_portability.py` checks local package containment, identity, dependency locks, Git pins and case-insensitive tracked paths without Unity caches. A fresh Unity import on Windows/macOS/Linux remains unverified: Editor launches are blocked by the active phone-session arrangement. No new renders or player builds.

Verification: current-source compilation passed for the five restored package runtime assemblies (including the LevelKit bridge), the four game layers and Editor/PlayMode test sources, using cached Unity references. This is compilation, not execution of Unity tests. The independent .NET 10 probe was not run: this host has SDK 9.0.203.

A clean Git archive of the committed repository also passed the portability check after extraction into a temporary workspace with no `Library/` or sibling repositories. Push attempts failed because this environment has no GitHub HTTPS credentials; changes are committed locally.

## Оптимізація після native audit — 2026-10-09

Каталог кампанії розбирається один раз, порядок рівнів повертається read-only; кеш скидається при імпорті контенту та початку Play Mode. Стежки використовують підготовлені сегменти замість повторного розрахунку на кожну травинку/точку снігу, з перевіркою змін даних рівня. Native EditMode: **13/13 Passed**, включно з покриттям листя й порівнянням clearance до/після in-place edits ([receipt](docs/performance/2026-10-09/optimized/editor-contracts.json)). GPU-листя, кеш native glade mesh і розподілене створення підлоги пройшли **8/8 PlayMode tests** ([receipt](docs/performance/2026-10-09/optimized/runtime-regressions.json)): shader coverage, recovery/input, forest coverage/pooling, map scroll/weather та 720×1600, 1280×800, 3440×1440 layouts. Low/Balanced shadow maps мають 512/1024 px, High — 2048. Замір до/після виконується наступним окремим native прогоном.

Перший післяоптимізаційний native pass: **7 832 кадри / 62 491 scopes**, окрема мапа **676 / 28 866**, обидва Passed, identity/save/ADB guards Passed. [Pass 1](docs/performance/2026-10-09/optimized/pass1/comparison.json) збережено як проміжний вимір: щокадрова побудова листя прибрана, але HTML/scene hitches лишилися, а початкове правило «одна плитка за кадр» подовжило маршрути. Це не фінальний успіх бюджетів; наступний pass усуває cold UI та перевіряє часовий бюджет підготовки світу.

Другий пакет пройшов **9/9 native PlayMode regressions**: підготовка світу частинами, дві forest-черги з бюджетом 4 мс, кеш форми дерев, рендер камери під завісою, скорочені CSS для меню й порожніх шарів, bounded DOM кнопок мапи. Перевірено збереження scroll viewport/позиції, видимі кнопки та запуск останнього рівня з оновленого native button. Свіжі QA captures меню, мапи, налаштувань і табору створено; повторний performance pass — наступний крок.

Кеш бонусних галявин і скорочений CSS мапи пройшли **14 EditMode / 9 PlayMode** tests. Геометрія всіх бонусних слотів порівняна з початковим projection у трьох позиціях, включно з UV wind roots. Проміжний замір віртуалізації (до CSS/bonus cache) збережено в [pass2/roadmap](docs/performance/2026-10-09/optimized/pass2/roadmap/summary.json): cold open 419 мс, але scroll spikes потребували наступного виправлення.

Розширена перевірка після оптимізації HUD: **13/13 native PlayMode tests Passed**, включно з native callback/reconcile, pause/resume, German 130% text scale, delayed display layout та recovery після навмисної mount failure. HUD використовує derived CSS 10.7 KB; порожній camp overlay не створює HTML context. Порівняльна performance матриця виконується після цього перевіреного checkpoint.

Повторний pass до стабільних DOM keys: routes Menu→Camp max 150 мс, Camp→Menu max 207 мс (baseline 735/959), [повна матриця pass4](docs/performance/2026-10-09/optimized/pass4/summary.json). Виявлено, що anonymous батьківські map nodes змушували reconciler міняти всі дочірні кнопки при зсуві вікна. Stable IDs зберігають native identity перекриваних targets; цей контракт пройшов у **13/13 PlayMode** regressions. Menu world/atmosphere підготовлено в різні кадри після монтування UI; частковий прихований world render прогріває геометрію до reveal. Новий native performance замір виконується на цьому checkpoint.

Після стабілізації DOM keys мапа зберігає mounted targets, поки viewport лишається всередині overscan із запасом 200 px. Це прибирає зайві повні reconciliation на кожному кроці 420 px під час повільного scroll. **13/13 native PlayMode regressions Passed** після зміни; видимі targets, bounded DOM, identity, scroll position та останній gameplay callback перевірено. Повторний timing буде опубліковано разом із фінальним звітом.

Подорожі та їхній preview тепер використовують derived CSS **3 KB** замість повного **41.3 KB** stylesheet. Розширений native UI contract відкриває список, натискає справжню кнопку основної подорожі, перевіряє viewport і повернення через дві back-кнопки. **13/13 PlayMode regressions Passed**; shared CSS cascade/wide rules залишаються в generator.

Налаштування меню й табору використовують derived CSS **21 KB** замість **41.3 KB**, зі спільними control/icon/privacy/resource правилами. Native UI regression проходить доступні категорії та back controls, preview подорожей і gameplay pause. **13/13 PlayMode Passed**; QA capture також синхронізує language preference з українськими labels.

Fresh-checkout consistency: roadmap dioramas тепер індексуються тим самим campaign ID order, що й native кнопки, замість фізичного порядку всіх metadata records. Зайві frozen/QA summaries не подовжують головну мапу; відсутня decorative summary має нейтральний fallback без level/solver load. **13/13 PlayMode regressions Passed** з навмисно перемішаними in-memory records і зайвим QA записом, включно з перевіркою кожної видимої diorama ID. Файл користувача не змінено.

Фінальний замір оптимізації ([summary](docs/performance/2026-10-09/optimized/summary.json), checkpoint `e8dd7a0`): **7 386 кадрів / 55 198 scopes**, Passed. Leaf CPU p95 **17.34 → 0.03 мс**, лише 3 mesh calls на 194 анімовані кадри; найдовші route кадри **960 → 237 мс**; перше відкриття мапи max **1 377 → 303 мс**; HTML mount p95 **252 → 109 мс**; плавний drag мапи p95 **152 → 35 мс**. Бюджети ще не досягнуті для route max ≤100 мс, sweep/сталих сцен ≤33.33 мс і Levels open ≤100 мс; залишкова черга — в [карті продуктивності](PERFORMANCE_MAP.md). Чотири свіжі QA кадри мають [manifest з hashes](docs/performance/2026-10-09/optimized/images/manifest.json); інтерактивний report містить таблицю до/після й шість datasets.

## Перенесення моделей і продовження · 2026-10-09

За запитом користувача підготовлено [приватний пакет передачі, звіт](docs/TRANSFER-2026-10-09-UA.md) та [план для нового ноутбука](docs/CONTINUE-2026-10-09-UA.md). На момент пакування optimized snapshot і prepared composition `eb7ee6d` збережено окремо; злиття, native bake та acceptance нових моделей ще не були завершені. Три selected concepts з handoff включено як референси. UAC пакети перенесено як assets-only private backup, без credentials/audit tools та без автоматичного імпорту; catalogue-based candidates описані у звіті. Нових Unity renders/player builds ця передача не містить.

На новий запит про застосування передачі локально відновлено City, Meadow,
Swamp, Particle FX, Battle Royale 1.05 та вкладений Nature URP layer.
Abilities збережено окремо до встановлення Game Creator 2 за вибором
користувача. Старіший Battle Royale 1.04 має внутрішню помилку архіву.
GUID конфлікти виправлені з оновленням посилань; donor sources виключені
з Git. **10.10.2026 ізольований Unity 6000.6.2f1 AssetDatabase import
пройшов**: 1 156 FBX, 1 784 meshes, кампанія 110 ID.
[Звіт і точні receipts](docs/transfer/2026-10-09/IMPORT-UA.md) відділяють
цей імпорт від ще не виконаних native bake і перевірки вигляду.

Source composition pipeline, власні model kits і Editor preview додані до
цієї integration branch. Новий renderer ізольований у `UI/Prepared`; чинні
optimized map classes збережені. Prepared authoring охоплює 30 рівнів,
тому перед публікацією Main перевіряється точна відповідність усім 110
campaign IDs. Неповний або перемішаний каталог відхиляється. Код runtime,
Editor та наявних/staged tests компілюється; native acceptance і
перемикання меню ще не виконані. 25 концептів перенесені як референси.
