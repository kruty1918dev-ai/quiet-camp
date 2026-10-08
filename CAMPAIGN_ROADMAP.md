# Quiet Camp — карта світу та структура кампанії (релізний масштаб)

> Навігація: [ілюстрований довідник](docs/README.md) · [світ і source catalog](docs/world.md) · [галерея](docs/gallery.md) · [стан перевірок](docs/status.md).
>
> На 2026-10-08 committed main catalog задає 110 ordered places; цей документ описує релізний план ширшого світу. Кількість у source catalog не є заявою native/device acceptance всього контенту. Нова semantic roadmap composition лишається в окремій art branch до bake/visual acceptance.
>
> Native Editor performance audit, 2026-10-09: підтверджено важке leaf mesh, синхронну scene/UI activation та procedural map rebuild під час scroll. Дві performance fixtures і п'ять foliage regressions Passed; budgets не пройдені й оптимізації відкриті. [Виміри та порядок виправлень](PERFORMANCE_MAP.md) · [інтерактивний report](https://kruty1918dev-ai.github.io/quiet-camp/performance.html).


Ціль: ~300 рівнів, що відчуваються як жива карта світу з центральною
сюжетною дорогою та відгалуженнями — а не лінійні 1–300.

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

## Ритм

- **Кожен 5-й рівень** — story beat: після-рівнева картка історії
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
