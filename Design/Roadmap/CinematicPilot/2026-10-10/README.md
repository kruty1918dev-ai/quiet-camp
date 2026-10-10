# П’ять місць української долини · 10.10.2026

Поточна оптимізація тіней: 43 native кадри Passed; сітки мають на
22,7–22,9% менше вершин зі збереженням усіх трикутників і атрибутів.
Game View, відео та acceptance нижче поки представляють попередню ревізію;
повтор готується. [Дорога — до / після](../../StopRoad/2026-10-10/index.html).

Безперервний перспективний світ QC001–QC005: мозаїчна зупинка → сад,
що пережив дім → ліс уздовж дротів → очеретяна переправа → пошкоджений
сільський гідровузол. Рельєф, дорога, русло, сонце й вітер спільні.
На мапі п’ять дорожніх каменів та одна кнопка виходу.

[Native URP / Game View, переходи та відкриття](index.html) ·
[До / після](../../ModelRefinement/2026-10-10/index.html) ·
[Відео маршруту](camera-sweep.mp4) · [Hashes та provenance](acceptance-receipt.json)

## Художні зміни

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
прямокутники споруд. Low: 2215 рослин, 1049 пучків трави;
Balanced: 3100 рослин, 1392 пучків.
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

## Перевірки поточної ревізії

- Schema / semantic composer: **51 instances, два owned approaches, нуль diagnostics**;
  детермінізм, власність ансамблів, outdoor canopies, п’ять IDs, відсутність
  наметів та progress/access boundaries — Passed.
- **43 native кадри**, 2026-10-10T18:28:07 UTC: дев’ять композицій
  у Low/Balanced, стани відкриття, landscape, pinch-віддалення 15% та крайні
  положення. Shader errors відсутні, budgets перевірені.
- **Game View integration Passed**, 18:34:38–18:36:20 UTC, 101.33 с:
  новий профіль після onboarding, перемоги 1–5, gameplay returns, старий
  прогрес із пропусками, replay, synthetic notch, portrait/landscape,
  обидва tiers, reduced motion, переривання прольоту, тіні opaque об’єктів
  і відсутність перекриття детальної дороги дальньою землею.
  36 gesture samples перевіряють direct drag, pinch, normalized wheel,
  fractional trackpad, tap jitter та OS cancellation без додавання UI підказок.
- Чотири повторні входи після прогріву: **2 камери / 398 матеріалів /
  203 meshes** глобально в Editor на кожному вході; нових матеріалів немає.
- Вода рухається за нерухомої камери в обох tiers: середня RGB-різниця
  40×40 px ділянки за ~2 с — **2.63/255 Low /
  3.94/255 Balanced**.

[Native receipt](native-capture-receipt.json) · [Bake receipt](bake-receipt.json) ·
[Game View receipt](gameview-integration-receipt.json) · [Passed XML](integration-results.xml) ·
[Lifecycle](lifecycle-diagnostics.json) · [Executed assemblies](compiled-assembly-receipt.json)

## Бюджети й межі доказу

Подані у frustum meshes: максимум **98,775** трикутники Balanced /
**72,482** Low, до шести матеріалів і трьох детальних chunks.
Бюджети Balanced: 150 тис./120 draw calls/12 матеріалів; Low: 80 тис./80/8.
Native ресурсний відрізок займає близько 57.5 МБ на диску; це розмір assets,
не вимір runtime RAM. Геометрія не генерується під час скролу.

У 20 Game View samples максимум draw calls — **54 Low /
54 Balanced**; pass-inclusive triangles — **143,407 /
195,950**. Immediate batch UnityStats повертає нулі;
вони не використовуються для приймання draw calls. Editor counters не
означають mobile FPS або GPU timing. Фізичний телефон та фактичний звук
не перевірялись; player builds не створювались.

193 Game View PNG змонтовано при 24 кадрах/с у 8,04 с H.264 без звуку.
Захоплення зайняло 35.32 с зі змінним темпом; відео прискорене,
усі 193 кадри декодовано. [Video receipt](video-receipt.json).

`python3 tools/verify_cinematic_pilot.py` перевіряє джерела, runtime hashes,
PNG, budgets, повний Passed XML, lifecycle, відео й browser receipt без Unity.
`python3 tools/verify_roadmap_vegetation.py` незалежно перевіряє native посадки.
Три галереї, включно з порівнянням дороги, перевірені Chromium на ширинах 320/720/1440, обидва режими й tiers,
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
