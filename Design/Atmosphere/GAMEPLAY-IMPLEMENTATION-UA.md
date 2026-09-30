# Реалізація gameplay-атмосфери
Оновлено 30.09.2026 (друга ревізія). Цей файл описує поточне підключення у Unity. П’ять PROMPT-UA.md покрито реалізацією; лишається лише вимір на пристрої.

## Реалізовано
- Шість згенерованих текстур скопійовані в Resources/QuietCamp/Atmosphere/Textures: morning, noon, evening, night, foreground, rear.
- CampSceneHost створює CampAtmosphere після побудови поля й HUD. Сцени не потрібно вручну перебудовувати. Як і раніше, композиція сервісів починається з Boot; чистий запуск Camp без bootstrap окремо не додано.
- Одна камера малює весь екран. CameraFitter підбирає scale та зсув за BoardViewport; інпут продовжує використовувати ту саму камеру.
- Backdrop зберігає пропорції з aspect-fill. Дальній темний ліс обмежений краями, щоб не дублювати центральний горизонт.
- Передні кущі мають справжню alpha-прозорість і вже намальоване м’яке розмиття. Це один quad, без Depth of Field, повноекранного blur та додаткової камери.
- Shader маскує передній шар у projected bounds поля та наметів із запасом і м’якою зовнішньою межею. Нижній край rear-шару розчиняється в галявині, щоб не було прямокутного стику. У декоративних шарах немає активних колайдерів.
- Фаза доби задає directional light, ambient, тон переднього й заднього шару, інтенсивність колихання трави/квітів, птахів, цвіркунів і стан вогню.
- Ніч не запускає денних птахів. Повторне застосування тієї самої фази не дублює loop. Fire/crickets належать gameplay host і зупиняються при виході.
- Завершення рівня не перемикає ніч у вечір.
- Resize/safe-area layout оновлюють fit. Reduced motion прибирає рух близького шару й sway у локальних матеріалах рослин.
- Матеріали належать сцені; shared cache пакета Atmos не перефарбовується.
- Texture importer обмежений новою папкою: Clamp, Bilinear, mipmaps Off, Read/Write Off; Android/iOS ASTC6×6.

## Де налаштовувати
[atmosphere.json](../../QuietCamp/Assets/QuietCamp/Resources/QuietCamp/atmosphere.json) — єдине авторське джерело нових фаз. Редагуй profiles для освітлення/палітри/вітру/часу між птахами й levelPhases для відповідності рівень→фаза. day залишається alias для noon; невідоме значення має безпечний noon fallback. Дублікати/невалідні кольори/межі відхиляються при завантаженні.

Початкове чергування: QC001 morning, QC002 noon, QC003 evening, QC004 morning, QC005 night, QC006 noon, QC007 evening, QC008 morning, QC009 night, QC010 noon, QC011 evening, QC012 night. JSON самих рівнів, contentHash і save schema не змінені.

Для живого налаштування можна викликати CampSceneHost.SetAtmospherePhase("night") та інші підтримані ID. Це єдиний зовнішній вхід для одночасного оновлення освітлення/графіки/звукової фази; не викликати лише зміну текстури замість нього.

## Шейдер AtmosphereLayer
Шлях: Assets/QuietCamp/Shaders/AtmosphereLayer.shader. Збережений Resources material Layer.mat прямо посилається на shader, тому включення не покладається на один Shader.Find.

- URP SRPDefaultUnlit, один texture sample, alpha blend SrcAlpha/OneMinusSrcAlpha.
- ZWrite Off, ZTest Always, Cull Off; для backdrop/rear renderQueue1000/1010, для near3050.
- Камера очищує SolidColor; старий skybox у Camp не перемальовує фон поверх quad.
- _Tint — тон шару. _ProtectedRect — xmin,ymin,xmax,ymax у viewport UV. _Protection=1 тільки на near.
- _Feather задає м’яке наростання alpha назовні protected rect; усередині alpha0.
- _EdgeOnly=1 на rear звужує додатковий ліс до лівого/правого краю та плавно прибирає непрозору основу текстури.
- Немає depth sampling, noise textures, постійних temporary RT чи fullscreen convolution. Непрозорий фон і два transparent layers усе одно коштують fill-rate; це треба заміряти на телефоні.
- HUD залишається на штатному Canvas, поверх world. Фон не стає raycast target.

## Спільний вітер і пориви (промпт 01/03)
- `Infrastructure/WindSim.cs` — чистий C# симулятор: напрямок XZ, базова сила, огинаюча пориву (attack .8s / hold .5–1s / release 1.5–2s), каденція за фазою (20–45s, ніч 35–70s), окремий seeded `System.Random` — gameplay `UnityEngine.Random` не торкається.
- `CampAtmosphere` володіє єдиним `WindSim`, що оновлює шейдерні параметри централізовано (`_WindDir/_WindStrength/_WindGust/_WindPhase`) для всіх foliage-матеріалів; алокацій матеріалів у кадрі немає. Подія `GustStarted` споживається аудіо й частинками з того самого знімка.
- Деревам у `DecorSpawner` додається `FoliageSway`; пакетний кеш матеріалів ключований (color+amp+freq), тож дерева й трава з однаковим кольором не ділять чуже налаштування. Коріння стабільне: деформація лише крони height-маскою шейдера. Reduced motion прибирає амплітуду.
- Напрямок вітру змінюється плавно (lerp до цілі), spatial phase — від world position уздовж напрямку.

## Частинки (промпт 03)
- `AtmosphereParticles` створює фіксовані емітери: leaf drift (атлас `leaves.png` 2×2, шейдер `QuietCamp/LeafParticle`), пил/пилок, світлячки (лише вечір/ніч), дим-іскри від вогню — без particle lights, collision, trails, distortion, noise textures.
- Бюджети за якістю: Low ~8 / Balanced ~24 / High ~36 активних частинок; Low вимикає пилок і світлячки, але листя й дим лишаються — сцена візуально повна.
- Емісія призупиняється поза viewport (culling), перехідні частинки окремі від gameplay.

## Аудіо (промпт 02)
- Усе йде через наявний `AudioService` — пули, буси, `PlayAt` для позиційних джерел. Нових паралельних систем немає.
- `AudioHandle` несе generation token: пул переиспользує той самий `AudioSource`, і застарілий handle не може зупинити/масштабувати новий playback (регресійний PlayMode-тест проганяє 60 циклів пулу).
- Per-key cooldown у каталозі поглинає спам (`ui.click` 80мс тощо); voice budget — декоративні one-shot пропускаються першими, loop-шари йдуть завжди; `MaxSimultaneous` обмежує кожен ключ.
- Echo-tail hold: джерело з `EnableEcho` не повертається в пул в кадр Stop — bounded wet-tail window (delay×(1+4·decay), 0.3–1.5с), хвіст не зрізається. Pause-guard: `AudioListener.pause` не «завершує» призупинені сорси.
- Фазові ваги bird/crickets/owl + `windAudio` (ранок .75/полудень 1/вечір .65/ніч .45) беруть той самий phase snapshot, що й світло. Сова розкладена на вечір/ніч; порив вітру (`GustStarted`) синхронно запускає `ambience.gust` + `sfx.rustle` з окремими cooldown'ами — один звук на подію, без дублів.
- Перші ~10с після входу в сцену — calm window без сови й гучних акцентів; декоративні one-shot мовчать під час переходів, паузи й модальних вікон.
- Один `AudioListener`-proxy над центром поля; камерний listener вимкнено, 3D-джерела пануються за екранною орієнтацією; anchors не повторюються підряд, позиційний pitch jitter відділений від gameplay RNG (seeded за `decorSeed`).
- Завершення рівня — один головний акцент `level.complete` + duck буса (target .75, attack .1s, release .9s); `sfx.chime` максимум один раз; фаза доби не перемикається.
- Імпорт: `importLoadType`/`importCompression` з `audio_catalog.json` реально застосовуються `MvpContentBuilder` через `AudioImporter` (Streaming для loops, DecompressOnLoad для UI/one-shot); sha256 хеші в каталозі актуальні.
- Згенеровані кліпи: wind bed (процедурний — Moyva-джерело виявилось суб-чутливим DC-дрейфом), crickets loop (truncating cyclic crossfade, шов 0.005), owl hoot (+echo 180мс), wind gust, leaf rustle, twig snap (onset-рампа, DC-очищено), chime. Виміри — [AUDIO-MEASUREMENTS.md](AUDIO-MEASUREMENTS.md); це PCM-метрики, не доказ сприйнятої якості петлі.

## Перехід FoliageDive (промпт 04)
- `FoliageDiveTransition` — єдиний перехід у `ScreenRouter` (Doors/Iris/Curtain для сцен більше не використовуються). Стани Idle→Covering→CoveredLoading→Preparing→Revealing→Idle, помилка/timeout→Recover з гарантованим звільненням та генераційним токеном проти воскресіння застарілих очікувань.
- Усі таймінги, глибина камери, палітри й бюджети листя — у `Resources/QuietCamp/transition.json`, завантажується через `TransitionConfig` (validate→resolve→freeze); у коді дублів немає.
- Readiness-контракт замість довільних кадрів: reveal дозволено лише коли `IsReady` нового scene host (рівень прочитано, world/HUD/camera fit/фаза застосовані) і під cover пройшов хоча б один намальований кадр; обидва очікування обмежені wall-clock і ведуть до recovery.
- Камера занурюється уздовж −baseline up на `cameraDepthFraction×H`, size→`cameraSizeMultiplier`; reveal стартує з `revealDepthFraction` і повертає точний fit. Під час CoveredLoading camera не пишеться — fit сцени може оновити baseline; при resize baseline перезнімається на тому самому normalized progress.
- Листя: 2/4/6 спрайтів за якістю, нижньо-домінантна хореографія зі stagger-затримками, scale-зростанням і кутами ±6–10°; reveal рухає той самий normalized progress, тож листки реально відходять, а не зависають. Розміри від короткої сторони канваса — без розтягнутих прямокутників на планшеті; відсутній спрайт → дешевий dissolve-fallback.
- Палітра за цільовою фазою: під непрозорим cover колір тонує до тіні нової сцени (ніч #132D38, ранок/полудень #203D36), листя бере фазовий tint — reveal уже показує узгоджену фазу.
- Ввід: CanvasGroup raycast + `InputPolicy.AcquireBlock(All)` + Modal-контекст із блокуванням нижніх хоткеїв; активний drag скасовується без commit (`PlacementController` реагує на блок); Back проковтується виділеним no-op `qc.transition.back`.
- Звук: вхідний rustle на normalized marker ~0.5 (gain .3, pan −.12), вихідний на ~0.28 (−4.5 дБ тихіше), амбієнт dip −2 дБ через scoped `DuckBus`. Повторні тапи ігноруються (`IsBusy`), timeout ~15s показує локалізовану підказку й recovery без скасування самого Unity-load (операція дочікується під `IsBusy`, тож запізніла активація не конфліктує з retry).
- Reduced motion: камера й листя нерухомі, cover 140мс → readiness → reveal 180мс, без cues; той самий readiness/error/input lifecycle.
- Blur: свідомо не реалізований RenderGraph-прохід — High використовує Balanced fallback (baked-soft foliage), як дозволено специфікацією; `blurHigh` у конфізі лишається false.

## Час доби та пост-ефекти (промпт 05)
- Фаза в `atmosphere.json` — авторські дані рівня, не годинник. Єдиний snapshot живить світло, небо, звук, частинки, вогонь і пост.
- `PhasePostFx` володіє runtime Volume: Low — жодного пост-проходу; Balanced/High — лише стримане color adjustment. Bloom/HDR/blur не ввімкнено — профілювання на пристрої має це підтвердити ([URP mobile notes](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/integration-with-post-processing.html)).
- Глибина/opaque textures не вмикаються; якість змінюється з гистерезисом, не коливається покадрово.

## Сумісність із паралельним переносом
Під час роботи локальні FireFx/Sky/Foliage шейдери перенесли у Kruty1918.Atmos. Цей перенос збережено. Новий код використовує актуальний FireVisual пакета; його API не змінювався. Трава/квіти лишаються на Atmos/FoliageSway з локальними material copies й налаштованими _BaseColor/_SwayAmp.

## Перевірка
Звіти Unity зберігаються в ігнорованій папці quiet-camp/Temp/ai/atmosphere/ поза Unity-проєктом: Unity очищає власний Temp при наступних запусках. Перевірено компіляцію, **66 EditMode** і **27 PlayMode** тестів. PlayMode охоплює фази, наявність шарів, підтримку shader, відсутність рожевих пікселів, недублювання loop, атлас 2×2 і бюджети частинок, спільний вітер, єдиний listener-proxy, dive-перехід із відновленням вводу, вихід у меню й повернення реальною кнопкою Start/Continue, recovery без воскресіння CoveredLoading, ретракцію листя на reveal, policy-гейт і reduced-motion гілку переходу; аудіо — усі 17 ключів каталогу, пріоритети UI>ambience, stale-handle після рециркуляції пулу, cooldown-поглинання, bounded echo-tail і нульову гучність на Master=0.

Кадри з камери Unity показують world без overlay HUD; це не рекламні рендери й не повні device screenshots. Тести в Editor не є перевіркою fps/нагріву/ASTC на Android чи iPhone. Окрему Android-збірку, яку паралельно виконував інший процес, не зараховано до перевірок цієї зміни.
