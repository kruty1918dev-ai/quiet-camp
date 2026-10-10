# П’ять місць української долини · 10.10.2026

Художній прохід: 43 native кадри оновлено 2026-10-10T17:56:07 UTC.
Поточний bake пройшов незалежну перевірку повних контурів рослинності,
берега й будівель. Максимум геометрії: 74 043 Low / 99 513 Balanced,
шість матеріалів. Game View і відео нижче ще належать попередній ревізії;
повтор готується. [Добір моделей і порівняння](../../ModelRefinement/2026-10-10/README-UA.md).

Чинну роадмапу замінено безперервним перспективним 3D-світом QC001–QC005.
Мозаїчна зупинка → сад, що пережив дім → ліс уздовж дротів → очеретяна
переправа → тиха вода біля пошкодженого гідровузла. Один рельєф, дорога,
русло, світло й вітер з’єднують місця. На мапі п’ять дорожніх каменів та
одна кнопка виходу; наметів і сюжетних панелей немає.

[Галерея native URP / Game View, переходи та прогрес](index.html) ·
[Відео маршруту](camera-sweep.mp4) · [Підсумкові hashes і provenance](acceptance-receipt.json)

Gameplay-моделі, наявні пазли, повні кампанія/альбом/збереження та права
доступу залишаються в даних. Пілот обмежує запуск QC001–QC005 через
Continue, Next та replay; старий прогрес понад п’ять відкриває весь відрізок
без переписування completed IDs. Нові пазли й player builds не створювались.

## Що перевірено

- JSON Schema та portable composer: п’ять IDs, повні ансамблі, детермінізм,
  власність входів, відсутність рослин всередині руїн, відсутність наметів,
  межі запуску й старий прогрес.
- Native URP: дев’ять композицій у Low і Balanced, чотири стани відкриття,
  два широкі кадри, п’ять landscape anchors, десять Low/Balanced portrait кадрів при віддаленні 15% та чотири крайні положення огляду.
  Shader errors відсутні. Геометричні й material/residency budgets перевірені.
  Деталізація та сам URP render profile відповідають Low/Balanced у receipts.
- Повний Game View integration **Passed**, 17:07:21–17:09:11 UTC: цикл п’яти перемог і повернення,
  старий прогрес із пропусками, replay, drag без запуску, synthetic notch,
  portrait/landscape, Low/Balanced, reduced motion та переривання прольоту.
  Початковий fixture — свіжа кампанія після onboarding; сам вступ має окремі тести.
- Чотири повторні входи/виходи після прогріву з однаковими глобальними
  counts **2 камери / 406 матеріалів / 230 meshes** у кожному вході; нових матеріалів між входами немає. Це глобальні Editor counts після кожного повторного входу на мапу.
- Пробник реального domain/session/save pipeline: 14 contract groups Passed;
  110 main, 143 journey та 21 bonus puzzles пройшли validator, saved witness
  й independent solver. Це перевірка збережених даних, не відкриття решти кампанії.

[Native receipt](native-capture-receipt.json) · [Bake receipt](bake-receipt.json) ·
[Game View receipt](gameview-integration-receipt.json) · [Passed XML](integration-results.xml) ·
[Lifecycle diagnostics](lifecycle-diagnostics.json) · [Video receipt](video-receipt.json)

## Межі вимірів

Геометрія поданих у frustum meshes: максимум **147 092** трикутники Balanced
і **78 628** Low; до п’яти спільних матеріалів та трьох детальних частин.
Бюджети: Balanced 150 тис./120 draw calls/12 матеріалів; Low 80 тис./80/8.
Native assets з двійковими meshes займають близько 65 МБ. Immediate batch
`UnityStats` повертає нулі й не використовується як вимір draw calls.
Game View `UnityStats` включає render passes і UI; його triangles відрізняються
від бюджету геометрії світу. У 20 Game View samples максимум draw calls — **42 Low / 42 Balanced**; pass-inclusive triangles — **141 231 / 268 859** відповідно. Editor counters не означають mobile FPS чи GPU timing.

`python3 tools/verify_cinematic_pilot.py` звіряє authoring/runtime hashes, PNG, budgets, повний Passed XML, lifecycle, відео й browser receipt без запуску Unity.

193 PNG з native Game View змонтовано при 24 кадрах/с у **8,04 с** відео.
Захоплення PNG відбувається зі змінною швидкістю; відео прискорене, без звуку.
Захоплення зайняло **33,94 с**. Час захоплення та SHA-256 кожного вихідного кадру збережені у video receipt.

`-noaudio` передано, але вимкнення аудіопристрою не підтверджене.
Фактичне звучання й mobile performance не перевірялись. Повторні невдалі
Editor-запуски перед прийманням збіглися з `sys-guard SIGTERM` при RAM <10%;
FMOD/Mono errors спостерігались під час завершення. Ці запуски не є Passed.
Окремий невдалий QA-запуск виявив застарілий shared `Library/ScriptMapper`: Boot не знаходив клас компонента. QA-клон має власні ArtifactDB, SourceAssetDB, ScriptAssemblies, ScriptMapper і Bee, Boot — правильний serialized class binding, а `IPrebuildSetup` перевіряє й оновлює native script imports до Play Mode; preflight оновлює mappings перед повним flow. Shell-обгортку додатково перевірено `bash -n` і isolation probe після виправлення її пост-run помилки.
Захист, shared Editor preferences та сторонні телефонні сесії не змінювались.

## Авторинг і наступні зміни

Джерела: `QuietCamp/Assets/QuietCamp/Authoring/Roadmap/Composition/CinematicPilot`.
Використовуйте [inspect → guarded patch → validate → dry-run → native bake](../../../../tools/scene-composition/USAGE_UA.md).
Generated meshes оновлюються повторним bake, а не ручним редагуванням.

Краї огляду доповнено 14 деревами з gameplay-набору: групи продовжують
дорогу до першої зупинки й берег за греблею. Baker використовує кожен
прийнятий landmark один раз; повторне додавання source landmarks до
composer output вилучено.

Тіні також враховують camera boom: мапа тимчасово збільшує shadow distance активного URP tier та відновлює його при виході. Shared матеріали й renderer settings не копіюються на кожен вхід.

Native publication: **Quiet Camp → Cinematic Roadmap → Bake Five Places**.
Перевірена приватна PID/proc/network + masked-USB ізоляція:
`bash tools/render_diorama_editor_isolated.sh --probe`, далі `--cinematic`
або `--pilot-tests`. Один Editor, workers 1/4, перевірка RAM та місця перед запуском.

Після уточнення користувача камеру віддалено на **45%**: authored distances 128–151 замість 88–104. Оточення стає додатковим джерелом історії: сусідні ансамблі читаються в одному кадрі, без надмірного віддалення. Дальній рельєф продовжено до Z=260 для portrait pinch. Початок імли залежить від camera boom, тож висота не знебарвлює весь світ.

Portrait FOV 30°, нахил авторських anchors 54–55°. Landscape обмежує
горизонтальне поле зору, щоб при pinch-віддаленні не показувати край землі.
Мозаїка, руїни й камені мають авторські коридори видимості; дальній ліс і
суцільне русло лишають контекст поза детальними частинами. Прогрес визначає
відкриття; swipe/drag/scroll рухають камеру, pinch змінює відстань ±15%.

Перетягування прив’язане до точки торкання й фактичної перспективи: світ
іде за пальцем, без залипання або прискорення біля локацій. Колесо нормалізоване
до mouse notch, fractional trackpad events зберігаються. Коротке тремтіння
пальця не скасовує tap; drag, pinch, другий палець та OS cancellation
не запускають рівень. Невеликі межі огляду `−0,25…frontier+0,20` дозволяють
оглянути першу місцевість у новому профілі, зберігаючи progress/reveal.
Рух пальцем одразу перериває автоматичний проліт; reduced motion після
перемоги одразу фокусується на новій точці. Пояснення й нові UI controls на
саму мапу не додавались.

Галерею перевірено локально в Chromium на ширинах 320, 720 та 1440 px: усі зображення, обидва render modes і quality profiles, metadata відео та відсутність horizontal overflow. [Browser receipt](browser-receipt.json).

Це перевірена основа для продовження; оцінка художньої якості лишається
дизайнерською. Старі D01–D12, atlas та performance screenshots зберігають
свої дати й не представляють поточну мапу. Початкову сторонню зміну
`QC_Renderer.asset` збережено й не включено до commits.

## Рух води й берег

Річка використовує встановлений Stylized Water 3 (3.2.7), простий режим
шейдера, чотири шари River Wave Profile та грановані нормалі.
Налаштування автора — [water.json](../../../../QuietCamp/Assets/QuietCamp/Authoring/Roadmap/Composition/CinematicPilot/water.json):
невисокі хвилі, рух нормалей і нерівномірна вузька берегова піна з ripples.
Поверхня продовжена під схили берега; її фактичний контур задає scene depth,
а не край прямокутної сітки. Частота вершин підвищена, mesh має tangents
і безперервні метричні UV уздовж вигнутого русла. Авторське вирівнювання
садиб не перекриває дно річки.

Берегова піна обмежена 0,11 світового метра: ширше значення покривало
майже все мілке дно. Відблиски, прозорість і глибина замінюють попередню
пласку м'ятну заливку. Напрям течії збережено; шум нормалей, піна й
хвилі не додають бурхливої морської погоди. SSR, окремі planar-камери,
refraction і caustics не потрібні цьому виду з висоти.

Два додаткові запечені варіанти green-channel transparency приховують воду
разом із майбутньою місцевістю для frontier 2/3. До річкового відрізка
вона не відображається; після QC005 використовується повне русло. Геометрія
під час руху камери не генерується; жодні shared Renderer preferences
для цього не змінюються.

Рішення звірені з документацією автора: [Shader](https://staggart.xyz/unity/stylized-water-3/sw3-docs/?section=shader-2),
[Waves](https://staggart.xyz/unity/stylized-water-3/sw3-docs/?section=waves-2),
[River mode](https://staggart.xyz/unity/stylized-water-3/sw3-docs/?section=river-mode-2),
[Performance guidelines](https://staggart.xyz/unity/stylized-water-3/sw3-docs/?section=performance-guidelines-6).
Онлайн-документація описує 3.3.2; API/параметри додатково звірені з локальною
3.2.7.

Окремий повторний прогін також виявив старі QA DLL через спільний Bee.
Його кадри/відео не прийняті як доказ останніх source changes. Bee тепер
приватний, launcher перевіряє п’ять mutable caches, а фінальний прогін
має waterMotion та renderProfile у своєму receipt. Попередні докази
замінюються поточним підтвердженим проходом.

Фінальний Game View flow: 17:07:21–17:09:11 UTC, 109,44 с; native capture:
16:57:20 UTC. Для нерухомої камери різниця RGB у 40×40 px ділянці
річки за ~2,16 с — 3,92/255 Low і 2,77/255 Balanced. Це перевірка руху
поверхні, не фізична симуляція чи вимір mobile FPS.
[Хеші виконаних QA assemblies та джерел](compiled-assembly-receipt.json)
зафіксовані окремо від native compilation: різні проєкти можуть давати
різні байти DLL. Фінальний receipt містить поточні probes waterMotion
та фактичні URP renderProfile, а mutable Bee більше не спільний.
