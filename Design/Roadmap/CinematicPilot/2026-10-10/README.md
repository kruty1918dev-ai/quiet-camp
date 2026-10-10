# П’ять місць української долини · 10.10.2026

Рельєф і світло оновлено та перевірено: 43 native кадри 19:10:30 UTC і повний
Game View flow 19:14:47 UTC Passed. [Об’єм — до / після](../../DepthReview/2026-10-10/index.html)
показує п’ять місць і чотири переходи з тими самими авторськими camera anchors.
Відео та receipts відповідають поточній ревізії.

Безперервний перспективний світ QC001–QC005: мозаїчна зупинка → сад,
що пережив дім → ліс уздовж дротів → очеретяна переправа → пошкоджений
сільський гідровузол. Рельєф, дорога, русло, сонце й вітер спільні.
На мапі п’ять дорожніх каменів та одна кнопка виходу.

[Native URP / Game View, переходи та відкриття](index.html) ·
[До / після](../../ModelRefinement/2026-10-10/index.html) ·
[Відео маршруту](camera-sweep.mp4) · [Hashes та provenance](acceptance-receipt.json)

## Художні зміни

Чотири широкі складки рельєфу підходять до дороги й потрапляють у видиму
частину portrait кадру. Дно долини та вирівняні двори відділяються від схилів;
до води веде низька заплава. Направлене прохолодне світло неба та тепле
сонце на 34° описують грані дерев і дахів. Дальній план пом’якшується,
слабка низька імла біля води залишає крони виразними. Нових realtime lights,
shadow maps, SSAO чи об’ємних particle passes немає.

На native перевірках уточнено занадто крутий перший берег, прогалини coarse
terrain уздовж русла й тонкі LOD шви. Берегові tiles деталізуються офлайн;
їхня висота дна збігається з детальною землею. Короткі запечені крайові
полоси закривають стики різної деталізації, без нових renderers.
[Художній огляд і датовані порівняння](../../DepthReview/2026-10-10/README-UA.md).

Дев’ять виразніших моделей обрано з наявного каталогу після візуального
огляду. Збережено гранований ліс gameplay; сад, прибережні дерева,
криниця, лавка, зламана огорожа й хата мають власні силуети та палітри.
Конкретний prefab визначає mesh/LOD і матеріал; кольори атласу запікаються
у vertex colors. Окремі LOD та collision meshes виключені.
[Рішення і відхилені кандидати](../../ModelRefinement/2026-10-10/README-UA.md).

Рослинність планується в усьому світі до поділу на chunks. Повні контури
крони із запасом для вітру не входять у воду, архітектуру, дорогу та
підходи. Два дерева перенесено з руїн назовні. Трава, кущі й дерева також
виключають одне одного: мінімум 0,15 між консервативними контурами в координатах авторингу
(0,06 Unity units після масштабу світу 0,4).
Незалежний пробник перевіряє пари, берег у 32 точках контуру й повернуті
прямокутники споруд. Low: 2223 рослин, 1052 пучків трави;
Balanced: 3118 рослин, 1404 пучків.
[Clearance receipt](vegetation-clearance-receipt.json).

Біля зупинки двосмуговий асфальт із вицвілою центральною й крайовою
розміткою, світлими узбіччями та невеликою заїзною кишенею. Майданчик
очікування з’єднано з нею; зупинка повернута до дороги, лавку й мозаїку
видно з керованої камери. Перша позначка стоїть на узбіччі. Асфальт
повторює фактичні трикутники землі в обох tiers; спрощений дальній
рельєф вимикається над завантаженою детальною землею. Кишеня й майданчик
виключені з посадок. Далі дорога плавно стає зарослою ґрунтовою колією.
Бічні схили, прохолодні світлі тіні й запечене контактне затемнення
додають маси. Уся непрозора геометрія відкидає тінь від одного сонця.
Чинні shadow atlas 512/1024 залишаються; додаткових тіньових камер немає.
Невеликі готові сітки пилку рухаються у shader: до 18/36 частинок на chunk,
без CPU particle simulation. Reduced motion приховує пилок.


## Тіні без додаткових проходів освітлення

Усі непрозорі об’єкти та рослинність, включно з травою, кущами, огорожами,
камінням і спорудами, мають ShadowCaster. Один сонячний shadow pass і один
каскад: atlas 512 Low / 1024 Balanced, без тіней додаткових lights.
Колірний і тіньовий проходи використовують однаковий вітер.

Запечені сітки об’єднують тільки абсолютно однакові позиції, нормалі,
кольори, UV та дані кореня/висоти рослини. При bake перевіряється кожен
елемент розгорнутого triangle stream. Це зберігає грановані силуети й
палітру; Low містить на 22.89% менше вершин, Balanced — на 22.64%.
Сітки з менш ніж 65 536 вершинами використовують 16-бітні індекси.
Сума vertex/index payload двох варіантів п’яти chunks — 35.59 МБ;
це дані buffers, не вимір RAM/GPU телефону. Історична перевірка indexing-only зміни о 18:43:32 UTC
показала pixel equality статичних кадрів зупинки в обох tiers.
[Її receipt](../../DepthReview/2026-10-10/before-shadow-budget-receipt.json)
зберігає дату й не означає рівність нових художніх кадрів.
Вода й прозорий пилок не створюють непрозорої тіні на землю.

[Shadow budget receipt](shadow-budget-receipt.json). Менше дубльованих
вершин зменшує вхідні дані для основного, depth і shadow проходів;
це не вимір GPU часу або обіцянка однакового FPS на всіх телефонах.

## Перевірки поточної ревізії

- Schema / semantic composer: **51 instances, два owned approaches, нуль diagnostics**;
  детермінізм, власність ансамблів, outdoor canopies, п’ять IDs, відсутність
  наметів та progress/access boundaries — Passed.
- **43 native кадри**, 2026-10-10T19:10:30 UTC: дев’ять композицій
  у Low/Balanced, стани відкриття, landscape, pinch-віддалення 15% та крайні
  положення. Shader errors відсутні, budgets перевірені.
- **Game View integration Passed**, 19:13:08–19:14:47 UTC, 98.86 с:
  новий профіль після onboarding, перемоги 1–5, gameplay returns, старий
  прогрес із пропусками, replay, synthetic notch, portrait/landscape,
  обидва tiers, reduced motion, переривання прольоту, тіні opaque об’єктів
  і відсутність перекриття детальної дороги дальньою землею.
  36 gesture samples перевіряють direct drag, pinch, normalized wheel,
  fractional trackpad, tap jitter та OS cancellation без додавання UI підказок.
- Чотири повторні входи після прогріву: **2 камери / 360 матеріалів /
  192 meshes** глобально в Editor на кожному вході; нових матеріалів немає.
- Вода рухається за нерухомої камери в обох tiers: середня RGB-різниця
  40×40 px ділянки за ~2 с — **4.67/255 Low /
  6.56/255 Balanced**.

[Native receipt](native-capture-receipt.json) · [Bake receipt](bake-receipt.json) ·
[Game View receipt](gameview-integration-receipt.json) · [Passed XML](integration-results.xml) ·
[Lifecycle](lifecycle-diagnostics.json) · [Executed assemblies](compiled-assembly-receipt.json)

## Бюджети й межі доказу

Подані у frustum meshes: максимум **99,642** трикутники Balanced /
**73,026** Low, до шести матеріалів і трьох детальних chunks.
Бюджети Balanced: 150 тис./120 draw calls/12 матеріалів; Low: 80 тис./80/8.
Native ресурсний відрізок займає близько 40.9 МБ на диску; це розмір assets,
не вимір runtime RAM. Геометрія не генерується під час скролу.

У 20 Game View samples максимум draw calls — **55 Low /
55 Balanced**; pass-inclusive triangles — **144,496 /
197,685**. Immediate batch UnityStats повертає нулі;
вони не використовуються для приймання draw calls. Editor counters не
означають mobile FPS або GPU timing. Фізичний телефон та фактичний звук
не перевірялись; player builds не створювались.

193 Game View PNG змонтовано при 24 кадрах/с у 8,04 с H.264 без звуку.
Захоплення зайняло 34.85 с зі змінним темпом; відео прискорене,
усі 193 кадри декодовано. [Video receipt](video-receipt.json).

`python3 tools/verify_cinematic_pilot.py` перевіряє джерела, runtime hashes,
PNG, budgets, повний Passed XML, lifecycle, відео й browser receipt без Unity.
`python3 tools/verify_roadmap_vegetation.py` незалежно перевіряє native посадки.
Чотири галереї, включно з порівняннями рельєфу й дороги, перевірені Chromium на ширинах 320/720/1440, обидва режими й tiers,
завантаження зображень, video metadata та відсутність horizontal overflow.
[Browser receipt](browser-receipt.json).

## Авторинг та інтеграція

Джерела: `QuietCamp/Assets/QuietCamp/Authoring/Roadmap/Composition/CinematicPilot`.
[inspect → guarded patch → validate → dry-run → native bake](../../../../tools/scene-composition/USAGE_UA.md).
Generated meshes змінюються через джерела та повторний bake.
Меню: **Quiet Camp → Cinematic Roadmap → Bake Five Places**.

Одна ізольована Editor сесія, приватні PID/proc/network, прихований USB,
перевірка RAM і місця, workers 1/4: `render_diorama_editor_isolated.sh --probe`,
потім `--cinematic` або `--pilot-tests`. QA має окремі save/product identity
та приватні Bee/ScriptAssemblies/ScriptMapper/ArtifactDB/SourceAssetDB.
DLL двох compilation projects fingerprinted окремо; byte equality не є
вимогою. Старі невдалі або stale-cache запуски не подаються як Passed.
Спільні Editor preferences й телефонні сесії не змінювались.
Початкові bytes стороннього `QC_Renderer.asset` збережені та виключені з commits.

Камера з висоти пташиного польоту: віддалення на 45%, distances 128–151,
pitch 54–55°, portrait FOV 30°, pinch ±15%, guided route без обертання.
Landscape FOV обмежує видимі краї рельєфу; імла враховує camera boom.
Direct drag тримає світ під пальцем. Endpoint margins не відкривають
нові місця: progress визначає reveal та доступ. Після перемоги показується
новий відрізок; свайп перериває рух, reduced motion одразу фокусується.
При виході camera/light/atmosphere/audio та shadow distance відновлюються.

Річка: встановлений Stylized Water 3 3.2.7, чотири River Wave Profile layers,
грановані нормалі, metric UV, depth-based bank intersection, вузька берегова
піна 0,11 м, невисокі хвилі та ripples. Джерело —
[water.json](../../../../QuietCamp/Assets/QuietCamp/Authoring/Roadmap/Composition/CinematicPilot/water.json).
Поверхня заходить під схили; плавний витік і detailed bed не перекриті
coarse horizon. Вода має baked frontier transparency та відкривається
разом із річковим відрізком. SSR, refraction, caustics і planar cameras вимкнені.
Налаштування звірені з локальними shader sources і документацією автора:
[Shader](https://staggart.xyz/unity/stylized-water-3/sw3-docs/?section=shader-2),
[Waves](https://staggart.xyz/unity/stylized-water-3/sw3-docs/?section=waves-2),
[River](https://staggart.xyz/unity/stylized-water-3/sw3-docs/?section=river-mode-2),
[Performance](https://staggart.xyz/unity/stylized-water-3/sw3-docs/?section=performance-guidelines-6).

Gameplay-моделі, наявні пазли, повні кампанія/альбом/збереження та права
доступу зберігаються. Пілот обмежує QC001–QC005 через Continue/Next/replay;
старий прогрес понад п’ять відкриває відрізок без переписування completed IDs.
Художня оцінка лишається дизайнерською. Історичні D01–D12, atlas та
performance screenshots зберігають свої дати й не представляють цю ревізію.
