# Єдиний промпт для агента: живий і затишний світ QuietCamp

> Скопіюй агенту цей файл повністю. Він містить самодостатнє завдання; інші Markdown-файли для розуміння вимог не потрібні.

## Завдання агента

Ти працюєш над QuietCamp — мобільною low-poly головоломкою про тихий кемпінг. Доведи атмосферу gameplay й меню до узгодженого, спокійного вигляду: багатошаровий ліс, об’ємні рослини, м’який вітер, рідкі частинки, камерні переходи крізь листя, природний звук і делікатні постефекти. Підтримай ранок, полудень, вечір і ніч. Усі підсистеми повинні реагувати на один стан світу.

Потрібно реалізувати й перевірити поведінку, а не тільки додати порожні класи чи описати план. Працюй у перевіреному актуальному коді, зберігай паралельні зміни. Не змінюй правила головоломки, contentHash рівнів, свідчення розв’язків або формат сейвів заради косметики. Не оголошуй непротестовані бюджети виміряною продуктивністю. Наведені нижче числові параметри — стартові налаштування для контрольованого tuning.

### Художній результат

Галявина має відчуватися тихим місцем, куди хочеться повернутися. Чітке поле — у центрі уваги. Далекий ліс трохи темніший і прохолодніший; близькі кущі темні й м’яко розмиті. Ранок світлий і ніжний, полудень теплий без кислотної зелені, вечір із локальними золотими акцентами, ніч — синьо-зелена з читабельним полем і теплом справжнього вогню. Не накладай помаранчевий фільтр на все.

Рух повільний, небагатослівний і причинно пов’язаний: один порив похитує крони, траву та квіти, може підняти один листок і викликати один тихий шелест. Між подіями є паузи. Звук не повинен заповнювати кожну секунду. UI мінімальний і контекстний: постійно доступна навігація/пауза, необхідний стан вибору; підказки з’являються за реальною потребою. Текст чіткий, поза blur і grading. Нічого важливого не передавати тільки кольором або звуком.

### Актуальна основа, яку треба зберегти

Стан перевірки — 30.09.2026; перед роботою звір його з репозиторієм. Цільовий Unity-проєкт: quiet-camp/QuietCamp, Unity6000.6.2f1, URP17.6.0. Шляхи Assets/ і Packages/ нижче відносні до нього.

У коді вже підключено:
- Assets/QuietCamp/Scripts/Infrastructure/AtmosphereCatalog.cs: читання, валідація та незмінні профілі з Resources/QuietCamp/atmosphere.json.
- Assets/QuietCamp/Scripts/Presentation/World/CampSceneHost.cs: SetAtmospherePhase — спільний вхід для фазового фону, світла, вогню й звукових умов.
- Assets/QuietCamp/Scripts/Presentation/World/CampAtmosphere.cs: scene-owned backdrop, rear і near, зміна фази, маска поля, локальні копії foliage materials, cleanup.
- CameraFitter: camera.rect повноекранний, size/position підганяються під BoardViewport. CampHud резервує область між header і нижніми панелями.
- Assets/QuietCamp/Shaders/AtmosphereLayer.shader та Resources/QuietCamp/Atmosphere/Layer.mat: один sample текстури, alpha blending, protected rect, крайова й нижня fade-маска дальнього шару. Без постійного DOF.
- Ранок/полудень/вечір/ніч вже вибираються з JSON. day сумісний із noon. Для QC001..QC012 є авторські levelPhases overrides; не переписувати level JSON.
- Завершення рівня більше не примушує ніч стати вечором; ніч не планує денних птахів; fire/crickets handles зупиняються при виході.
- FireVisual, Campfire, Sky і FoliageSway перенесено в Kruty1918.Atmos. Не відновлюй старі видалені QuietCamp shader/types та не створюй їхні дублікати.
- Runtime аудіокаталог досі ScriptableObject AudioCatalog.asset. JSON audio_catalog.json містить метадані11 джерел і не підключає конфігурацію автоматично.
- Наявний ScreenRouter досі використовує Doors/Iris/Curtain. Новий camera/foliage transition нижче є завданням на реалізацію.
- Повний спільний gust scheduler, нові частинки, анімація об’ємних дерев, 3D-аудіо/echo й offline-виправлення WAV ще потребують реалізації.
- Композиція сервісів штатно починається з Boot. Не припускай, що чисте відкриття Camp без bootstrap створює всі сервіси.

Історичні заміри fps, скриншоти чи готові рендери не є доказом поточної мобільної продуктивності. Перевірені попередні17 EditMode і5 PlayMode тестів — лише стартова регресійна база, не заміна тестам твоїх змін.

### Один власник стану

Розширюй чинний AtmosphereCatalog і CampSceneHost/scene-owned presentation. Якщо виділяєш окремий controller, перенеси туди відповідальність повністю; не залишай двох авторів світла/доби. Використовуй існуючий GameServices/bootstrap та AudioService. Не вводь новий глобальний singleton і не обминай канонічний роутер/аудіопул.

Snapshot: phaseId, phaseBlend, sun/ambient, background pair/blend, windDirectionXZ, baseWind, gustEnvelope, fireActive, bird/cricket/owl weights, particle weights, quality tier, reducedMotion. Runtime-читачі не змінюють snapshot.

Час за замовчуванням художньо задається рівнем. Game-calendar підключати як upstream authority лише якщо продуктова логіка вже вимагає перебігу доби. Не синхронізувати примусово з системним часом і не створювати другий календар. Стабільні ID morning/noon/evening/night; legacy day→noon.

Конфігурація: JSON → Validate → Resolve → Freeze → Consume. Нові параметри додавай до чинного JSON-контракту або одного пов’язаного валідованого JSON, без дублювання тих самих значень. Runtime material/Volume instances — виконавчі об’єкти. Існуючий AudioCatalog SO мігруй тільки цілісно з перевіркою GUID/ключів; поки міграції немає, саме він керує звуками.

### Наявна графіка

Runtime: Assets/QuietCamp/Resources/QuietCamp/Atmosphere/Textures/.
- morning.png, noon.png, evening.png, night.png — кожен1086×1448, непрозорий backdrop;
- foreground.png —1086×1448, справжній alpha, близькі м’які кущі;
- rear.png —1536×1024, alpha, далекий ліс.
Додатковий atlas: Design/Atmosphere/assets/leaves.png від кореня репозиторію,1254×1254, RGBA, сітка2×2. Клітинка627×627: верхній лівий зелений широкий лист; верхній правий оливковий вузький; нижній лівий гілочка; нижній правий бурштиновий лист. Unity Sprite Rect рахує y знизу: верхній ряд y627, нижній y0. Це чотири форми, не flipbook.

Картинки вже згенеровані: не замінюй їх новими без художньої причини. Денні variants не є pixel-perfect relights; контролюй ghosting при blend. Не малюй інтерактивне поле/намети/текст у backdrop. Перевіряй композицію саме з реальними mesh і UI.



## 1. Постефекти, доба й мобільний бюджет

Мета — багатий, спокійний вигляд при малому GPU/CPU навантаженні. Тепло виникає передусім із палітри, світла, матеріалів і звуку; постефекти лише завершують сцену.

### Поточні налаштування
Перевірено Assets/QuietCamp/Settings/URP:
- QC_Low: renderScale.85, MSAA1, main shadow map512.
- QC_Balanced: scale1, MSAA2, shadow1024.
- QC_High: scale1, MSAA4, shadow2048.
- В усіх: supportsHDR0, requireDepthTexture0, requireOpaqueTexture0, mainLightShadowsSupported1, shadowDistance20, один cascade.
- QC_GlobalVolumeProfile: Bloom активний, але intensity0; ColorAdjustments активний і нейтральний.
Це serialized assets; фактичне призначення renderer/pipeline, camera post-processing і platform Quality mapping перевірити перед реалізацією. Сам факт існування трьох asset не доводить, що телефон обирає правильний.

### Власник доби
Увесь світ читає один snapshot. Немає окремого Update у світла, звуку й частинок із незалежним визначенням «ночі».
Default: художня фаза рівня, не реальний годинник. Якщо є game-calendar, використати його лише як upstream authority за відповідною ігровою вимогою; не вигадувати дві доби.
Підтримати morning, noon, evening, night; day alias→noon. Unknown/null → noon з одним diagnostic warning. Existing evening лишається evening. Не перезаписувати QC001..QC010 автоматично.

Поточне завершення рівня вже не змінює фазу доби. Збережи це; опційний celebration accent — локальний вогник/тепле світло на0.5–1с без примусової зміни night→evening. Будь-яка художня зміна часу має бути явною командою єдиному власнику стану.

### Вихідні художні параметри
Числа є базою для tuning на реальній сцені; hex задані sRGB, shader mixing linear. Kelvin не підміняє color і не застосовується двічі. Light intensity — Unity directional, не фізична гарантія lux.

| Параметр | Ранок | Полудень | Вечір | Ніч |
|---|---|---|---|---|
| Background | morning.png | noon.png | evening.png | night.png |
| Main light color | #FFE3BE | #FFF0DB | #FFC38D | #ACC6DE |
| Main light intensity | .80 | 1.00 | .55 | .28 |
| Main elevation (ілюстративно) | 28° | 50° | 18° | 30° |
| Ambient tint | #AABFB5 | #B9C9CB | #78939C | #627F94 |
| Base wind | .18 | .25 | .12 | .08 |
| Bird weight | 1 | .65 | .15 | 0 |
| Crickets weight | 0 | 0 | .30 | 1 |
| Owl weight | 0 | 0 | .15 | .65 |
| Fire warm accent | Якщо fireActive | Якщо fireActive | Якщо fireActive, виразніше | Якщо fireActive, локально |
| Mist/пилок | Ледь помітні на дальньому краї | Мінімум | Вимкнені | Вимкнені |
| Світлячки | 0 | 0 | Помірно | Рідкі |
| White Balance Temperature* | +3 | +1 | +5 | −3 |
| Saturation* | −3 | −4 | −2 | −7 |
| Contrast* | +2 | +3 | +2 | 0 |
| Bloom intensity* | .04 | .02 | .08 | .06 |

*Параметри URP Volume, коли quality їх дозволяє. Не накладати всі теплі поправки агресивно: фон уже має свою фазову палітру. PostExposure почати0 для всіх; темну ніч виправляти світлом/ambient, а не глобальним підйомом gamma. Колір правил, небезпечні/валідні стани й текст мусять лишатися розрізнюваними. Red tent і green ground мають розділятись також тоном/контуром.

Напрям main light узгодити з upper-right освітленням PNG в проєкції camera yaw225°. Наведена elevation — художня пропозиція, не готовий EulerX для довільної rotation. Ночі не потрібне фізично точне положення місяця, потрібна стабільна читабельність.

### Список ефектів і рішень
| Ефект | Low | Balanced | High | Навіщо / обмеження |
|---|---|---|---|---|
| Базові палітра/ambient/direct light | Так | Так | Так | Основний вигляд, не залежить від post |
| Color Adjustments + White Balance | Off базово; optional якщо GPU дозволяє | М’яко | М’яко | Один URP grading pipeline, не багато custom blits |
| Bloom | Off | Off за замовчуванням | Optional | Лише вогонь/локальні emissive, не молочний серпанок |
| Tonemapping | None LDR | None LDR | Neutral якщо свідомо ввімкнули HDR | Не ставити ACES для «кінематографічності» без A/B |
| Vignette | Графічне листя | Графічне листя | Optional intensity≤.08 | Не затемнювати всі кути двічі |
| DOF | Off | Off | Off gameplay | Near blur вже у PNG |
| Motion Blur | Off | Off | Off | Transition directional blur окремо і коротко |
| Chromatic Aberration | Off | Off | Off | Не допомагає спокою/чіткості |
| Lens distortion | Off | Off | Off | Не деформувати puzzle grid |
| Film grain | Off | Off | Off | Немає шуму й shimmer на малому дисплеї |
| SSAO / SSR / screen-space fog | Off | Off | Off | Об’єм через геометрію й дешеві контактні тіні |
| Volumetric rays/clouds | Off | Off | Off | Далекий фон уже містить світлову композицію |
| Screen-space full-color copy | Off | Off | Лише optional transition | Не тримати opaque texture заради неіснуючого ефекту |

URP уже має Volume-based postprocessing; PostProcessing Stack v2 не потрібен і не сумісний з URP. Unity називає color grading, vignette та Bloom без High Quality Filtering відносно придатними для mobile, але це не обіцянка вартості на конкретному телефоні. [Офіційна документація URP](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/integration-with-post-processing.html).

### Bloom і HDR — явна розвилка
Нині HDR вимкнений. Не встановлювати threshold>1 у LDR і дивуватися відсутності свічення. Базовий шлях: LDR + готовий FireGlow, bloom0, всі якості виглядають завершено.
Опційний High: ввімкнути HDR у погодженому pipeline/camera, підготувати emissive fire materials (значення>1), Neutral tone mapping; Bloom threshold≈1.05–1.2, intensity за фазою .02–.08, scatter.45–.6, HQ filtering Off, dirt texture Off, max iterations орієнтир4. Перевірити існування полів у встановленому17.6.
Вартість HDR render targets, MSAA resolve і bloom заміряти РАЗОМ, а не тільки bloom shader. Якщо HDR+MSAA4 занадто дорогі, High може лишитися LDR; не знижувати чіткість поля заради слабкого halo. Не форсувати HDR на всіх devices.

### Volume та матеріали
Не змінювати sharedProfile asset у Play Mode для доби. Runtime instance один на сцену/owner, dispose після сцени; дозволені components кешовані. Не створювати profile/material щокадру. State interpolator змінює значення тільки під час fade/реальної зміни; steady-state не rebuild LUT без потреби. Grading корекції не мають кодувати фазу вдруге поверх повністю тонованого світу.

UI Screen Space overlay після grading; видимий world-space текст, якщо він є, перевірити на контраст у всіх фазах. Меню та gameplay використовують той самий profile policy. Перехід через menu не скидає mute/quality/reducedMotion.

У Camp фон уже застосовується через CampAtmosphere; SkyPalette тепер делегує пакету Atmos. Перевір точну реалізацію sky refresh, не викликай DynamicGI.UpdateEnvironment щокадру. Переважно явний ambient; probe/environment refresh лише на безпечній точці за доведеної потреби. Не створюй два різні горизонти одночасно.

### М’яка зміна фази
Для preview/debug:10с color/light blend, audio6–10с, particles плавно змінюють rate, існуючі доживають. Interpolate sun direction без різкого оберту через0/360; для різних night/day directions під cover поставити нову позу. Wind base strength smoothing3–5с.
Коли новий рівень має іншу фазу — встановити її під opaque transition, а не кросфейдити всю галявину під пальцем. На seamless довгому crossfade AI-зображень можливий ghosting крони: це не одна і та сама 3D-сцена. Тому canonical change між рівнями робиться під cover. Не вважати довільне проміжне phaseBlend коректним часом, якщо audio/sky пресети не готові.

### Мобільні бюджети
Числа нижче — цілі приймання, що потребують замірів.
Low:30fps, render scale.8–.85 як fallback, 1×MSAA; нуль post passes; один directional, лише важливі shadows512 або контактні substitutes; no extra light shadows.
Balanced:30fps стабільно, scale.9–1,2×MSAA, shadow1024 короткого радіуса; color grading тільки за ресурсом; bloom off.
High:60fps лише коли p95 вкладається в16.7мс, scale1,2×MSAA спочатку;4× тільки за вимірами. HDR/bloom optional.
Орієнтир додаткової атмосфери відносно чистої сцени: CPU≤.5мс steady state, GPU≤1мс на погодженому середньому пристрої; **це не гарантовані виміри**. На Low спершу зменшити transparent coverage й зайві shadows, а не ламати UI.

Пам’ять: один source1086×1448 RGBA≈6MiB, без mipmaps, до компресії; два≈12MiB. Leaves1254² ще≈6MiB без компресії. Read/Write додає CPU-копії — Off. ASTC має інші фактичні витрати, перевірити Memory Profiler; не рахувати PNG file size як GPU memory. Один quarter-resolution RT має1/16 pixels повного, але world color intermediate/HDR/MSAA можуть коштувати більше самого blur. Не обіцяти «майже безкоштовно».

Quality визначити один раз із підтриманого tier/fallback, дозволити ручний override. Adaptive degradation тільки з hysteresis: наприклад перевищення frame budget10с → один рівень нижче; повертати вище не раніше60с і не під час drag/transition. Розрізняти CPU/GPU bound: scale знижувати тільки коли допомагає. Не запускати тест продуктивності кожен кадр і не перемикати HDR посеред interaction.

Порядок зменшення навантаження: optional blur → bloom/HDR → пилок/near particles → transparent coverage → декоративні shadows → render scale. Зберегти input, text resolution, sound feedback. Ui raycast canvas не rebuild через кожен gust.

### Перевірки
1. Frame Debugger: count passes/RT/copies до й після; Low справді без post/depth/opaque copies, крім тих, які потребують інші наявні системи.
2. Профілювання Development build на телефоні, не лише Editor:10–15хв; p50/p95 CPU/GPU, GC/alloc, thermal, audio DSP і memory.
3. Однакові screenshots чотирьох фаз на3 qualities; ніч читабельна за зниженої яскравості, glow не перекриває rule indicators.
4. Sleep/resume, rapid debug phase switch,30 scene transitions; немає material/profile leaks та безмежного росту streaming voices.
5. Довгий static idle без GC allocations від atmosphere scheduler; events не працюють кожен frame через LINQ/strings.
6. OpenGLES/Vulkan/Metal підтримувані поточним build — shader variants/import компресія перевірені. Unsupported compression має fallback.
7. Звук, вітер і particles насправді реагують на той самий snapshot; зміна ночі не лишає денних птахів, старого світла чи денного задника.

## 2. Фон, глибина, світло й шейдери

Мета: чітка інтерактивна 3D-галявина між темним далеким лісом і м’яким близьким листям.

### Композиція й порядок шарів
1. Full-screen opaque backdrop: morning/noon/evening/night.png, чотири варіанти одного краєвиду. Це задній малюнок; не перетворюй його на клікабельну геометрію.
2. rear.png — окрема темна лісосмуга для делікатного паралаксу й приховування стику. Використовуй лише там, де не з’являється подвійний горизонт: крайові сегменти або заміна частини намальованих дерев. Вимкнення цього шару є допустимою Low-оптимізацією.
3. Реальний world: підлога/земля, поле, намети, дерева й декор. Зберегти канонічні координати й правила. Клітинки не малювати у фоні.
4. foreground.png: близькі кущі, темні та вже оптично м’які. Вони декоративні й не приймають інпут.
5. Context UI — чіткий, без grading/blur поверх тексту. TransitionCover вище всіх шарів лише на час переходу.

Збережи вже реалізований fullscreen CameraFitter: світ рендериться на весь екран, поле вписується в BoardViewport за scale і зсувом камери. Інпут використовує цю саму проєкцію. Декоративні шари не перехоплюють raycast. Не повертай camera.rect до cropped viewport й не вводь camera stack заради одного фонового PNG.

### Геометрія без «листівки»
Наявна camera orthographic, нахил 55° і yaw225°. Не міняй її на перспективну лише заради краси: hit test і fit прив’язані до неї. Дерева середнього плану мають залишатися mesh-об’єктами з кількома об’ємами крони, нормалями й боковим світлом; PNG-дерева придатні для далекого шару.

Фон має горизонт приблизно у верхній частині, порожня галявина займає нижню більшу частину. Реальна межа лісу у згенерованих файлах приблизно y=40% від верху. Центр захищати за фактичним projected board bounds, а не за жорстким відсотком. Розширити його на 12–20 logical px; жодна декоративна непрозора деталь не заходить у цю область.

Foreground-файл містить нижній кущовий масив: не розтягувати його поверх низу поля. Посунути нижче/розвести кутові сегменти; застосувати м’яку layout-маску навколо protected rect шириною 24–40 logical px. Маска впливає лише на декор. Не маскувати клітинки чи об’єкти гри. Зовнішні краї виходять за кадр на 4–8%, щоб під час руху не відкривалися прямі краї PNG.

### Розкладка для різних екранів
У PNG співвідношення 3:4. На телефоні 9:19.5 режим Cover обрізає боки; це допустимо для фону, але може прибрати кущі. Тому фон і foreground НЕ використовують один спільний Fit:
- backdrop: aspect-fill, anchor по центру галявини; тестувати верх горизонту;
- foreground: кутові декорації прикріпити до лівого/правого краю фактичного екрана, низ до screen bottom; не до notch safe rect;
- world/інпут: лише safe BoardViewport;
- UI: safe area; на планшеті фон заповнює доступне, дерева рамкують світ, поле не розтягується.
Між землею world і намальованою галявиною — крайовий декор/м’який тональний перехід. Не маскувати помилкову перспективу туманом поверх усього поля.

### Світло й матеріали
Один directional light; тепла освітлена сторона, прохолодні тіні, мінімальна specular реакція рослин. Листя rough/matte, metallic0; не робити вологий пластиковий блиск. Для дерев 2–3 близьких кольори крони, нижній об’єм темніший на 10–20%. Variation детермінований від visual seed об’єкта, не щокадровий random.

На Low тіні можна замінити існуючими дешевими ground/contact sprites під декором. Якщо тіні потрібні для читабельності наметів — зберегти коротку directional shadow distance; пріоритет інтерактивним об’єктам. Не додавати SSAO, щоб компенсувати плоску геометрію. FireGlow — локальний теплий quad/земляний відблиск; максимум одна безтіньова локальна лампа на Balanced/High, тільки якщо потрібна й виміряна.

### Контракти шейдерів
Наведені нижче назви описують відповідальності. Чинний AtmosphereLayer вже виконує backdrop/rear/near через різні матеріали й параметри: розширюй його там, де це доречно, не створюй дубльованих шарів лише для відповідності назвам. FoliageSway/Fire використовуй із пакета Atmos; пакетні зміни повинні зберігати сумісність його інших споживачів.
**BackdropBlend**: URP unlit opaque, ZWrite Off, відомий порядок до world. Дві texture slots A/B + blend. У звичайному стані достатньо одного семпла; іншу текстуру підвантажити перед зміною. Blend у linear space після sRGB decode. Інтенсивність grading не застосовувати до вже тонованого PNG двічі: backdrop має окремий помірний tint (за замовчуванням білий), а world налаштовується під зображення.

**DistantTreeline**: unlit/tinted, transparent, ZWrite Off; одна texture, одна глобальна м’яка тональна поправка. На непрозорих середніх деревах використовувати opaque матеріал. Уникати великих накладених transparent full-screen quads. Паралакс лише від косметичного руху: rear 0.1×, middle 0.3×, near1×; idle максимальне зміщення фону 2–4 logical px, за reduced motion0.

**FoliageSway**: дивись розділ про рослини та частинки. Використати URP Core/Lighting, SRP Batcher-сумісний UnityPerMaterial; не підключати UnityCG як універсальне рішення для нового URP-коду. Forward/ShadowCaster/DepthOnly мають однакову деформацію. DepthNormals тільки якщо споживач справді потрібен; не вмикати depth texture заради відсутнього ефекту. Vertex displacement bounds розширені, щоб крони не відсікалися.

**NearFoliage**: один texture sample + tint, alpha blend. Вихідні PNG з прямим alpha: стандартний SrcAlpha/OneMinusSrcAlpha; якщо обрано premultiplied, RGB множити на alpha рівно один раз і Blend One/OneMinusSrcAlpha. Не називати alpha файл premultiplied без конвертації. Випробувати контур на світлому й темному фоні. Ніякого real-time DOF для цього шару.

**LeafParticle**: unlit tinted atlas, transparent без distortion, collision, soft particles і depth sampling. 2×2 atlas; кожна частинка вибирає один спрайт на старті, це НЕ flipbook анімація. Padding перевірити на mipmap/білі обводки.

**FireFlame/FireGlow**: перевикористати наявні. Спільна повільна envelope керує світлом, scale й sound gain, дрібне мерехтіння лише у полум’ї. Не мерехтить весь екран. Glow працює і без bloom.

**TransitionBlur**: тільки опційний High, контракт у розділі про переходи камерою; Low/Balanced використовують уже розмиту графіку.

### Імпорт
Фони: Texture2D, sRGB On, alpha не потрібен, Wrap Clamp, Bilinear; mipmaps Off для screen-space 1:1/fill. Максимальний розмір 2048 як вихідний орієнтир (фактичний source1086×1448, не робити upsample задля числа). Android/iOS ASTC6×6 за підтримки; Low8×8 тільки після перевірки градієнтів. Для непідтриманого формату задати сумісний fallback і заміряти фактичну пам’ять.

Foreground/rear/leaves: alpha з input, sRGB On, Clamp. Sprite multiple для leaves: чотири клітинки 627×627 в source1254², без обрізання вмісту. Якщо NPOT/компресія потребує padding, зберегти aspect і UV; не розтягувати листки. Screen-space mipmaps Off; world-space віддалені leaf sprites за потреби On і перевірити bleed. Read/Write Off після імпорту. Не тримати всі чотири некомпресовані фони постійно: активний+наступний під час crossfade, решту звільняти в рамках обраного asset lifecycle. Не викликати Resources.UnloadUnusedAssets посеред взаємодії.

### Приймання
Рівень лишається впізнаваним і прохідним без постефектів. Ранок м’який; полудень живий без кислотної зелені; вечір зігріває краї; ніч прохолодна, але не приховує правила. Немає плаваючих коренів, дубльованих горизонтів, прямокутних країв чи alpha fringe. Перевірити пальцем кожну крайову клітинку під час вітру. Художні відмінності між згенерованими lighting variants не вважати pixel-perfect relight: маскувати зміну під переходом, якщо довгий crossfade дає ghosting.

## 3. Рослини, спільний вітер і частинки

Мета: помітний зв’язок між явищами без безперервного декоративного шуму. Природа має паузи й різні масштаби реакції.

### Один вітер
Ввести одного власника WindSnapshot: directionXZ, baseStrength0..1, gustEnvelope0..1, phaseSeconds, visualSeed. Він частина atmosphere, а не незалежний singleton. Snapshot читають shaders, decorative particles та audio scheduler. Семантичні gust events надсилаються один раз, shader parameters оновлюються централізовано без створення material щокадру.

Visual clock незалежний від turn logic. На паузі може завмирати або йти повільніше згідно стану гри; за background зупиняється і відновлюється без стрибка phase. Не використовувати глобальний UnityEngine.Random для природи, якщо його бере генератор рівнів. Окремий seeded PRNG для декору.

Початковий вітер: morning.18, noon.25, evening.12, night.08. Порив +.15–.25, інтервал20–45с (night35–70с), attack.8с, hold.5–1с, release1.5–2с. Напрям змінюється на≤15° за30с, не стрибає. Це gentle breeze; немає бурі, сильних поривів чи постійного снігопаду листя.
Spatial phase = dot(worldXZ, direction)/wavelength − speed*time; wavelength8–14 клітинок, speed1–2 клітинки/с. Локальний gust може проходити сцену хвилею, але маленьке поле не має показувати довгу затримку між сусідніми рослинами.

### Об’єм дерева
Дерево — trunk + 2–4 об’єми крони з різною висотою/нахилом, стриманою різницею відтінку. Якщо модель уже достатня, зберегти її mesh; не створювати сотню окремих листків. Корінь й нижні30–40% стовбура майже нерухомі. Крона повертається/відхиляється над стабільним pivot; не ковзає по землі.

Три складові:
1. Ствол/основна гілка: max≈0.3–0.8° у спокої, до1.5° порив; частота .10–.18Hz.
2. Крона: бічне відхилення .5–1.2% висоти дерева у спокої, до2% порив; .18–.35Hz.
3. Легке коливання периферії: .1–.3% висоти; .7–1.2Hz, лише на outer leaves; не все дерево вібрує.

Початкові значення обмежити за конкретною моделлю. Весь mesh не розтягується як гума. Маска local normalized height:
h=saturate((localY-rootY)/max(height,epsilon)); bendWeight=smoothstep(.15,1,h)^2.
Для trunk нижче .35 ставити0 або дуже малу вагу. Для mesh різної висоти не використовувати поточну константу worldY/.6 як універсальну. Якщо модель має нестандартну вісь росту, вказати її в visual definition.

Vertex color R = основний bend weight, G = leaf flutter, B = branch phase, A зарезервований. Якщо канали зайняті, не перезаписати їх мовчки: окремий UV/підготовка меша. Fallback height mask допустимий для трави, але не має розгойдувати стовбур і квітку однаково.
Деформація у локальній системі, напрям world wind перевести правильно в object-space; не застосовувати двічі scale. Нормалі transform inverse-transpose, для істотного згину оновити напрям/похідні; на low-poly з малою амплітудою візуально перевірити допустиме наближення. Root не зміщується. Bounds охоплюють крайні відхилення.

### Трава, листочок, квітка
| Об’єкт | Відповідь на base wind | На порив | Затримка/характер |
|---|---|---|---|
| Трава | Вершина3–6% висоти, root0 | До10% | Легка й швидка; сусідні пучки зі spatial phase |
| Квітка | Стебло2–4%, голівка слідує | До7% | Трохи повільніше трави, м’який overshoot≤10% |
| Кущ | Верх1–2% висоти | До3% | Кілька близьких, але не однакових фаз |
| Велике дерево | .5–1.2% висоти крони | До2% | Повільний нахил, стовбур стабільний |
| Близький PNG-кущ | 1–2 logical px | До4px | Повільна трансляція/rotation≤.4°; без нових transparent layers |
| Вільний лист | Дрейф за вітром | Швидше на30–50% | Не змінює напрям миттєво |
| Полум’я | Невеликий нахил | До5–8° | Не дублює коливання трави й не гасне випадково |

Зберегти листки/пелюстки прив’язаними до стебла. Немає cloth/rigidbody на кожній травинці, Animator на кожній квітці чи Update на кожному дереві. Одні матеріали й mesh variants; GPU instancing там, де реально підтримує mesh/material pipeline. SRP Batcher та instancing — не магічне «ввімкнути все»: перевір Frame Debugger; MaterialPropertyBlock не вводити без оцінки впливу на batching. Детерміновану variation можна передати vertex colors/instance data або малою кількістю матеріалів.

### Партікли: що, де, коли
Числа — **максимальні одночасні частинки на всю сцену**, не emissionRate кожного emitter. c=ширина клітинки. Decorative sprites без collider/raycast.

| Ефект | Місце й поведінка | Low / Balanced / High |
|---|---|---|
| Листок у повітрі | Край галявини, з upwind боку; emission .04–.10/с; lifetime6–10с; speed.15–.35c/с; size.08–.18c | 2 / 4 / 6 |
| Близький м’який лист | Лише периферія екрана; не над клітинкою під пальцем; один раз25–45с | 0 / 1 / 2 |
| Пилок у промені | Край світлої галявини вранці/вдень; дрібний, opacity≤.15; без блискіток | 0 / 6 / 10 |
| Світлячки | Біля низьких кущів, evening/night; повільна траєкторія, glow цикл2–4с, фази різні | 0 / 3 / 5 |
| Іскри вогню | Над справжнім активним Campfire/FireVisual, не над кожним наметом | 4 / 8 / 12 |
| Дим вогню | 1–3 м’які quads, life2–4с, alpha≤.12; вітер нахиляє | 0 / 2 / 3 |
| Далекий серпанок | Один статичний/повільний mesh/quad за полем, morning; не ParticleSystem на весь екран | 0 / 1 шар / 1 шар |
| Листя переходу | Тільки TransitionCover, коротко і крупно | 2 / 4 / 6 |

Не складати максимуми без обмеження: загальний gameplay cap Low8 / Balanced24 / High36, transition cap окремий лише коли gameplay emission приглушено. При переповненні прибрати пилок, потім близький лист, потім частину світлячків; успішний commit і читабельність поля не страждають. Прозоре покриття важливіше самого count: overdraw перевірити на пристрої.

Цвіркуни — передусім звук. Не показувати літаючі «цвіркуни» як світлячків. Птах чується з крони, і це не потребує постійної 3D-зграї. Світлячки не з’являються під час кожного tap; невдача не викликає червоні іскри. Вогонь уже має embers — змінюй їхній бюджет, не створи другий emitter.

### Псевдопослідовність пориву
t0: власник планує gust із seeded delay.
t0..+.8: envelope зростає; трава на upwind краю нахиляється; тихий gust audio стартує один раз.
t+.2..+1.2: крони поступово реагують; один rustle у найближчому декоративному anchor, лише якщо cooldown вільний.
t+.4: за budget і cooldown може стартувати один лист із краю куща.
t+1.3..+3.2: вітер стихає; квітка м’яко повертається з малим overshoot.
Жодна підсистема не запускає свій випадковий порив незалежно.

### Технічна дисципліна
ParticleSystem pooling — невеликий фіксований набір, не Instantiate/Destroy на кожний leaf. Main thread оновлює один wind payload; material globals пише тільки власник сцени й очищує на dispose. При двох камерах не змінювати стан двічі на frame.
World-space симуляція для природних листків; screen-space/локальна для transition overlay. Нове поле не успадковує старі частинки. Зберігати random seed в межах поточного візуального сеансу достатньо; gameplay determinism не зачіпати. Cull bounds коректні, за екраном emission зупинити/пропустити, а не накопичити.
Без particle lights, collision, trails, distortion чи noise3D textures за замовчуванням. 2×2 leaves atlas вибирається випадково один раз. Flash/glow ніколи не робить весь екран яскравішим.

Reduced motion: trunk/canopy/трава амплітуда≤20% або0 за обраною опцією, camera drift0, near flying leaves0, flicker0; стабільне полум’я й контекстний feedback залишаються. Звукова атмосфера лишається, якщо user audio не вимкнене.

### Приймання
Хвиля вітру помітна, якщо придивитися; кожна рослина має вагу. На паузі/поверненні немає «вибуху» накопичених particles. Корені стабільні при різних scale; тіні рухаються разом із деревами; canvas/інпут чисті. Візуальний seed не змінює рівень. Один реальний gust відповідає одному аудіо-пориву. Low виглядає закінченим без пилку/світлячків.

## 4. Переходи камерою крізь листя

Відчуття: гравець нахиляється вниз, великі листки наближаються до об’єктива, м’яко закривають краєвид; нова галявина відкривається крізь зелень. Ніякого різкого падіння, teleport flash або шумного кіношного whoosh.

### Місце інтеграції
Канонічний ScreenRouter уже керує переходами, busy/input gate і завантаженням. Розширити його presentation transition, не створювати другого маршрутизатора. Doors/Iris/Curtain для звичайних переходів замінити одним FoliageDive, а не додати поверх. Пауза/налаштування — локальне відкриття панелі без scene plunge. Перший запуск — короткий reveal із cover, без удаваного занурення з порожньої сцени.

Дотримуватися state machine:
Idle → Covering → CoveredLoading → Preparing → Revealing → Idle.
Помилка/timeout/cancel → Recovery; всі lease/input/handles гарантовано відпускаються. Один власник камери в кожний момент.

### Нормальний timeline
Тривалість самого руху приблизно1.0–1.2с + довільне очікування завантаження. Числа задаються в конфігурації, не розкидані по coroutines.

| Час | Камера | Листя/екран | Звук/UI |
|---|---|---|---|
| 0–80мс | Зберегти поточну fitted pose/size/rect | Екран ще відкритий | Один ui.click якщо дія була кнопкою; input gate; cancel drag без commit |
| 80–260мс | Down .04..10 видимої висоти; orthoSize1→.97 | Близькі кущі починають підніматись | UI opacity1→0 за120мс; ambience dip максимум−2dB |
| 260–440мс | Down до.16H; sizeдо.94; tilt optional≤2° | 2–6 листків великі, краї м’які; cover opacity0→1 | Rustle .15–.3 на вході в листя, не оглушлива хвиля |
| 440–520мс | Поза зафіксована | Повністю непрозорий темно-зелений cover | Зафіксувати непрозорість перед scene load |
| 520мс…ready | Рендер старої сцени не потрібен | Opaque #173638, поверх нерухомі/дуже повільні великі листки | Crossfade старої/нової ambience; не крутити rustle у loop |
| ready+0–120мс | Новий fit готовий; початкова offset .10H, size.96 | Cover ще непрозорий | Перевір listener, кадр world і layout |
| ready+120–420мс | Smooth return до точного fit | Листя виходить вниз/вбік; cover1→0 | Один короткий rustle exit тихіше входу на3–6dB |
| ready+420–560мс | Fit без offset і drift | Near foreground у звичайному стані | UI з’являється; віддати input лише за готового активного поля |

H = видима висота світу 2×baseline orthographicSize. Down означає **screen-down відносно камери**, а не globalY: translation −baselineCameraUp × .16H. Зміщення є косметичним offset від CameraFitter pose, не зміною координат поля. Орто-камера не має перспективного zoom від руху forward, тому потрібні помірне зменшення orthographicSize й scaling near foliage1→1.15–1.25. Не переносити фізичну камеру крізь terrain.

Рух monotonic smoothstep/cubic ease-in-out; жодного elastic, bounce, camera shake чи різкого roll. Камера не долітає до чорноти раніше листя. Близький шар рухається швидше заднього. Далекий фон зсувається2–5px максимум; основний ефект створюють листки.

### Закриття й готовність сцени
Alpha PNG має отвори — він НЕ може гарантувати приховане завантаження. Потрібен окремий повністю opaque color cover на весь framebuffer, включно з notch/letterbox. Не чорнота, а темний колір лісу. Стан Covered дозволений лише коли opacity=1 і покрит весь screen rect. Под cover листя маскує перехід художественно.

LoadSceneAsync і readiness — різні речі. Один Task.Yield після load, як у поточному роутері, недостатній як контракт. Readiness підтверджує: scene root/service bindings готові; level loaded/validated; board render створено; CameraFitter застосований після UI layout; фаза/фон/світло узгоджені; аудіо не дубльоване. Після цього один відрендерений стабільний кадр за cover, тоді reveal. Якщо фон не завантажився, використовувати узгоджений solid-color fallback; не тримати нескінченний lock.

Wait>1.5с: маленьке «Стежка вже поруч…»/спокійний індикатор на cover, без fake percentage. Timeout орієнтир15с для повідомлення й доступного Retry/Назад. Unity scene load не завжди можна скасувати фізично: cancel відхиляє результат/наступні callbacks за generation token, а активну операцію коректно завершує/прибирає. Не показувати напівготову сцену. Після помилки — відновити придатний екран, розблокувати навігацію, зупинити свої sounds. Exception не лишає _busy=true.

### Blur: три рівні
Low: без full-screen blur. Готовий м’який foreground, один opaque cover,2 leaf sprites; зменшення opacity світу допускається лише під листям, не як окремий довгий fade.
Balanced: та сама техніка з4 leaf sprites і тонким edge darkening уже в графіці. Не додавати DOF/MotionBlur.
High (опційно після профілювання): transient downsampled directional blur world color, 1/4 width × 1/4 height, максимум два короткі separable проходи по5 tap. Сумарний видимий радіус≈4–8 logical px, зростає лише260–440мс, зникає до reveal completion. UI/текст/системні кнопки не блюрити.

URP17: RenderGraph pass з явними read/write resources; не читати й писати той самий texture handle. Якщо active color — backbuffer, спершу отримати валідний sampleable intermediate/copy; не семплити backbuffer як texture. Реальний RenderGraph API звірити з установленим17.6, бо приклади6000.0 не гарантують ідентичні сигнатури. Не вимикати RenderGraph заради legacy CommandBuffer.Blit. У звичайному gameplay feature не алокує RT і не змушує всі камери постійно render into intermediate. При camera stack/resize/orientation тимчасові targets і descriptor оновлюються коректно. Не додавати motion vectors чи depth texture для імітації простого руху.

Тест A/B: якщо effect не помітно кращий за вже розмитий near слой — вилучити High blur. Це опція, не умова затишку. [Unity Render Graph](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/render-graph-introduction.html) описує керування проходами й ресурсами.

### Звук переходу
Використовуй наявний sfx.rustle; спершу слуховий A/B із зниженим gain. ambience.gust — для пориву світу, не обов’язковий4с whoosh на кожному переході. На вході rustle може збігтися з природним поривом лише через загальний scheduler, не дві копії.
UI click сухий і короткий. Листя має просторовий рух лише в pan(−.15..+.15), не бігає зліва направо на100%. Doppler0. Echo/reverb на transition rustle вимкнути. Дві точки cue прив’язані до progress, guards від повторного спрацювання; при frame hitch не програвати пропущені cues пачкою. Звукові handles належать transition scope.

Під full cover старий ambience зменшується, новий наростає .8–1.5с; один і той самий wind можна зберегти без restart. Listener стабільний; анімація камери не робить fire раптом ближчим/гучнішим. На mute не грає нічого; ready/pending не залежать від кінця звуку.

### Переривання та доступність
Reduced motion: нерухома camera, без flying leaves, без blur; затишний color dissolve140мс cover + wait +180мс reveal, ті самі gates/readiness. Rustle можна пропустити, залишити один тихий UI cue.
Back/repeated tap під transition не запускає другий load. Одна явна політика: ігнорувати звичайні повторні переходи, системний Back обробити через route intent після безпечної точки, без queue10 сцен.
Application pause: timeline зупиняється, cover не зникає випадково; resume перевіряє поточний load/readiness. Resize: cover негайно підлаштувати, baseline fit перерахувати, повернути новий fit; не відновлювати старі екранні bounds.
Drag cancellation не ставить об’єкт у невідому клітинку. Виключений input не забирає доступ до recovery Retry/Back.

### Приймання
Записати відео з повільною анімацією й scrub кожного frame: немає flash, leaf cutout прямокутника, провалу під землю, накладання UI на завантаження. Direct Camp стартує нормально без попередньої меню-камери. Після30 циклів baseline camera не дрейфує; rotations/sizes/rect рівні актуальному fit; input працює, audio voices/RT не ростуть. Slow load5с і simulated failure перевірені. Телефон не зависає на анімації через shader compilation — потрібні варіанти підготовлені до першого переходу.

## 5. Звукова карта, 3D та акустика

Нижче розділені факти перевірки та бажана поведінка.

### Результат аналізу наявного
Сканування всіх доступних через `rg --files Assets` WAV/OGG/MP3 дало **23 файли**. Активний `AudioCatalog.asset`: 11 основних + 6 extra, усі SpatialBlend0, усі effects вимкнені. Шість резервних OGG Kenney не додані в каталог. PCM виміряно для всіх 17 WAV; OGG включено до прослуховування, але їхній PCM не декодувався. Вбудований у FBX звук/Library/PackageCache не входив у сканування.

Нижче в цьому файлі є таблиця вимірювань і повні шляхи до звуків. Це аналіз файлів та коду, не підтвердження якості на слух. RMS усього кліпу з паузами не є LUFS чи оцінкою суб’єктивної гучності. Прослуховування оригіналів і фінального міксу — обов’язковий етап реалізації.

Важливі висновки:
- Вітер24с: stereo44.1kHz, peak−2.85dBFS, RMS−9.61. Вогонь30с: stereo44.1kHz, peak−13.55, RMS−40.77. Різниця RMS31.16dB; поточні Volume .15/.20 її не компенсують. Не підняти вогонь на31dB автоматично: перевірити його активні фрагменти, піки й прослухати в міксі; спершу прибрати домінування вітру.
- Цвіркуни8с mono: peak близько0dBFS, 35 samples |x|≥.999; різниця крайніх samples петлі0.220184. Це ризик кліку при loop, а не гарантія його суб’єктивної чутності.
- Twig0.3с mono: 21 sample |x|≥.999; початок/кінець сильно різняться. Він одноразовий, тому великий seam не означає зіпсовану петлю; перевірити різкий початковий click та пікове обмеження.
- Wind seam0.000183, fire0.010529. Навіть малий endpoint delta не доводить безшовність: також оцінити зміну спектра/енергії в переході.
- Owl2.8с mono: peak−13.48, RMS−28.19. Gust4с mono: peak−5.07, RMS−21.56. Rustle0.7с mono: peak−2.51, RMS−19.09. Chime1.8с mono: peak−1.92, RMS−16.31.
- Усі11 SHA256 із JSON збігаються з оригіналами. Генеровані6 в JSON відсутні, але є у runtime SO. GetKeys() у QuietCampAudioCatalog зараз повертає лише основні sounds; узгодити перелік із реально доступними keys.
- Runtime читає Resources/QuietCamp/AudioCatalog.asset через QuietCampAudioCatalog. Редагування одного audio_catalog.json не підключає 3D чи extras.
- У поточному CampSceneHost bird уже вимкнений для night, phase scheduler читає профіль, fire/crickets мають scene-owned handles та cleanup. Збережи це, додай точне просторове розміщення/плавні gain-envelope. Bootstrap володіє постійним wind; не створюй другу копію й не використовуй StopAll для scene-only cleanup.
- У проєкті немає активної музики; це не дефект. Спочатку повноцінна природна тиша, без обов’язкової музичної петлі.

### Очищення до мікшування
Працюй із похідними файлами в окремій директорії, не змінюй ліцензійні оригінали без потреби. Для власних procedural clips можна виправити генератор і відтворювано перегенерувати саме ці clips після порівняння.
1. Crickets: прибрати перевантаження генератора ДО clamp, лишити headroom; зробити коректну seamless loop із перекриттям 80–160мс. Crossfade розрахувати як нову циклічну хвилю з правильним wrap і довжиною, не fade-in/out до тиші кожні8с. Перевірити10 повторів. Для корельованого матеріалу почати з linear crossfade; equal-power може підняти гучність.
2. Twig: зменшити peak, перевірити початкові1–3мс; не стерти характер удару надмірним fade. Не використовувати його як кожний UI-click.
3. Rustle/chime: перевірити хвіст, потрібен плавний вихід2–10мс лише якщо є обрив.
4. Fire/wind: слухове порівняння loop point та import-компресії; якщо потрібне overlap playback, дві голосові доріжки на петлю рахуються в бюджет. Перевага чистому offline-loop.
5. Master не перевантажується в найгіршому збігу completion+fire+gust+UI. Орієнтир пікового запасу міксу≥3dB, додатково перевірити true peak експортом/аналізатором якщо доступно. Дані цього пакета — sample peaks, не true peaks.

### Карта «дія → звук → місце → частота»
Гучність нижче: бажані **відносні стартові множники після clip trim**, а не готові source Volume для нинішніх файлів. Головний орієнтир — тихий комфортний мікс при низькій гучності телефона. Один жест має один основний sonic reply.

| Подія | Наявний key / джерело | Простір | Поведінка |
|---|---|---|---|
| Відкрити кнопку меню / підтвердити діалог | ui.click / Minimalist1 | 2D Ui | 1 раз; cooldown80мс; max2 voices; gain.35 |
| Назад / закрити | ui.back / Minimalist2 | 2D Ui | .30; не дублювати ui.click |
| Обрати намет/об’єкт | ui.select / Minimalist3 | 2D Ui | .28; тільки зміна selection, не кожний кадр hover |
| Поворот об’єкта | placement.rotate / Minimalist4 | 2D або легка панорама точки | .30; тільки підтверджений крок; cooldown70мс |
| Валідне розміщення | placement.commit / Wood Block1 | 3D у центрі поставленого об’єкта | .48; один event після commit; max2 |
| Undo | placement.undo / Wood Block2 | 2D Sfx | .35; один успішний undo; невдала дія без удару |
| Невалідна спроба | rule.invalid / Minimalist5 | 2D Ui | .22; throttle350мс; м’який, без alarm/buzzer |
| Рівень завершено | level.complete / Minimalist6 | 2D Ui | .45; один раз на completion, не кожне відкриття панелі |
| М’яке післязвуччя перемоги | sfx.chime / chime_soft | 2D Ui | .12 через180мс; опційно, якщо не конфліктує з Minimalist6 |
| Базовий подих лісу | ambience.wind / Wind Soft Loop | 2D Ambience stereo | Одна петля; низько в міксі; fade1.5–3с; не залежить від камери |
| Живе вогнище | ambience.fire / Fire Burning Loop5 | 3D біля FireVisual | Петля лише коли fireActive; fade.8–1.5с; max1 |
| Птах далеко в кроні | ambience.bird / Bird Chirp06 | 3D на одному tree anchor | Morning18–35с; noon30–60с; evening60–100с слабко; night off |
| Нічні цвіркуни | ambience.crickets / crickets_loop | 2D Ambience bed | evening fade-in .3 веса, night1; morning0; max1 |
| Сова | ambience.owl / owl_hoot | 3D поза полем | Night55–100с, evening90–150с; max1; перші15с сцени тиша |
| Порив | ambience.gust / wind_gust | 2D Ambience або плавна stereo панорама | Тільки на реальний gust envelope; мін.18с між; .20; max1 |
| Листя поруч / камера входить у кущ | sfx.rustle / leaf_rustle | Для рослини3D; для переходу2D | .15–.35, max2; transition володіє своїм handle |
| Торкнутися сухої гілки в декорі | sfx.twig / twig_snap | 3D відповідний anchor | Лише явна взаємодія, якщо вона існує; .10; cooldown2с |
| Перемикач налаштувань | резерв switch-a/b.ogg Kenney | 2D Ui | A/B за on/off після слухового добору; не вводити якщо ui.select вже підходить |
| Легке натискання другорядної дії | резерв tap-a/b, click-a/b.ogg | 2D Ui | Кандидати заміни, не додаткові шари поверх кожного кліку |

Не додавати вигадані дії до gameplay заради файлу twig. Якщо об’єкт не інтерактивний, достатньо рідкого rustle від вітру. Жодних випадкових кроків, голосів, дзвіночків біля гравця без видимої причини.

### 3D і AudioListener
Один активний AudioListener. Поточна camera стоїть на відстані20; стандартні minDistance1 для поля можуть зробити всі об’єкти тихими. Обери явно:
- рекомендовано стабільний listener proxy над центром поля, орієнтація узгоджена з camera right/forward; висота близько2 клітинок, коректна ортонормальна rotation;
- не додавай другий listener до proxy; перенеси/вимкни старий зі збереженням сценового lifecycle;
- cosmetic plunge камери не пересуває listener різко. Doppler0 для ambience/UI/декору.

Нормувати дистанції до розміру клітинки c: fire min2c/max12c, bird/owls min4c/max24c, rustle min1.5c/max8c. Це старт, перевірити на найменшому/найбільшому полі. Custom rolloff має плавно сходити до0 біля maxDistance; сам maxDistance у logarithmic mode не слід трактувати як жорсткий silence cutoff.

Позиційні короткі файли — mono після перевірки phase при downmix. Stereo bed вітру лишити stereo. Crickets mono не перетворюється на stereo, якщо просто подвоїти канал. Просторовість забезпечують окремі рідкі джерела й реальна панорама; не розмножуй8с запис по всіх кущах. 3D AudioSource без HRTF spatializer — це панорама/затухання, не гарантований бінауральний звук. Новий важкий spatializer у базовий план не входить.

**Особливість наявного API:** AudioService.PlayAt → PlayInternal примусово ставить spatialBlend=max(current,1), тобто1. Min/Max налаштовуються раніше, лише коли definition.SpatialBlend>0. Тому гібрид .3/.6 через цей метод не працюватиме як очікується. Встановити коректні3D definitions для positional sounds; для гібридного сценарію мінімально розширити канонічний AudioPlayOptions і перевірити тести. Не обходитись ручним GetConfiguredSource.Play без обліку handle/pool/bus. Не змінювати загальний package API так, щоб зламати інших споживачів.

### Відлуння і повітря
Ліс не має звучати як кам’яна кімната. UI, вітер, цвіркуни, близькі placement — dry. Low без echo/reverb. Balanced/High: для ОДНОГО далекого owl/bird опційно дуже слабке outdoor echo: delay90–130мс, decay.10–.18, wet.025–.05, dry1. Почати з вимкненого; ввімкнути лише після A/B у навушниках і mono speaker. Не використовувати Room preset із дефолту пакета. Не сумувати echo+reverb на кожній пташці.
Якщо spatial reflection не робить сцену кращою, лишити dry+distance filtering. Для далеких подій low-pass орієнтовно6–10kHz; близькі не глушити. Не робити raycast-оклюзію кожного аудіоджерела щокадру; за потреби контрольні anchors/рідкі перевірки2–4Hz. Відстань, м’який тембр і тиша дають глибину дешевше.

### Спільна доба, пориви й мікс
Вітер реагує на той самий WindSnapshot, що трава: low-rate gain smoothing150–300мс, жодного Play щокадру. Візуальний gust плавно наростає приблизно.8с, тримається.5–1с, згасає1.5–2с; clip wind_gust4с стартує біля початку з відповідною envelope. Один event планує також не більше1–2 rustle anchors; не кожне дерево грає окремо.

При добі crossfade ambient weights6–10с. Bird/off не обривати посеред chirp; заборонити наступний. Owl, який уже почався, завершується природно. У меню той самий state, але менше випадкових подій. На паузі: gameplay one-shots зупинити/не породжувати; ambience можна тихо лишити за чинною продуктовою логікою. При background audio suspend не продовжувати scheduler і не накопичувати чергу птахів.

Buses: Master→Ui/Sfx/Ambience/Music (music може бути порожнім). Користувацькі налаштування мають останнє слово: gain = userBus × clipTrim × eventGain × distance × environmentEnvelope. Автодак не підвищує mute. Completion коротко duck Ambience до.75 за100мс, hold500мс, release900мс; звичайний tap не гойдає весь ліс.

Максимум одночасних голосів як стартова політика: Low8, Balanced12, High16; резерв2 для Ui і2 для gameplay feedback. Wind1+fire1+crickets1, решта one-shots. Поточне max8 PER KEY не є глобальним бюджетом. Не красти критичний commit заради сови; ambient events за відсутності місця пропустити. Handle для кожної loop; повторний Apply(snapshot) ідемпотентний. Scene-owned loops відпускаються на виході, bootstrap wind — у свого власника. Пул скидає фільтри, spatialBlend, rolloff, parent, position і mixer group перед повторним використанням.

### Імпорт і перевірка
Short UI/one-shot — Decompress On Load, PCM/ADPCM відповідно до вимірів пам’яті/якості. Long ambience — Vorbis/streaming лише після оцінки Streaming CPU і loop seam;8с mono crickets часто дешевше декодувати один раз. Не вмикати streaming усім малим clips. Значення importer у JSON звіряти з реальними .meta; runtime SO і Unity importer мають різні обов’язки. [Unity AudioClip](https://docs.unity3d.com/6000.0/Documentation/Manual/class-AudioClip.html) пояснює компроміси load types. [Unity AudioSource](https://docs.unity3d.com/6000.0/Documentation/Manual/class-AudioSource.html) — spatial blend та параметри джерела.

Приймання:10 loop wraps у навушниках; quiet phone speaker; mono downmix; без звуку теж зрозумілі помилки/успіх; night без денного bird spam;30 переходів без дубльованого fire/crickets; profile voices/CPU; mute/background/resume; відсутні key/clip fail без exception і лог-спаму. Порівняти peak, короткий RMS активних сегментів і слухову гучність, не нормалізувати природу під однакову «стіночку».

### Походження
KenneyUI/License.txt явно CC0. Наявні UI Soundpack, NaPH та Wind Soft Loop у manifest позначені як user supplied Moyva assets: їх не можна автоматично оголошувати CC0 чи перевикладати як власний звуковий пак. Generated6 мають локальний procedural generator QuietCampAudioExtras.cs. Цей комплект містить посилання на оригінали, а не копії ліцензійних записів. Нових завантажень не потрібно для базової реалізації. Подальші природні варіації bird/rustle — опційні; додавати тільки з підтвердженим походженням, ліцензією й слуховою перевіркою.

## 6. Повний перелік аудіоджерел із вимірюваннями

Шляхи відносні до Unity-проєкту QuietCamp/. Для WAV: raw PCM sample peak/RMS, не LUFS; seam — абсолютна різниця крайніх samples. Для OGG вимірювання PCM не виконувалося. 11 hashes з авторського manifest збіглися; 6 procedural extras і 6 резервних OGG не мають заявленого SHA256 у цьому manifest. Файли під час аудиту не змінювалися.

| Ключ | Шлях | Тривалість, с | Канали | Sample rate | Peak dBFS | RMS dBFS | Seam Δ |
|---|---|---:|---:|---:|---:|---:|---:|
| ambience.wind | Assets/Moyva/Audio/Ambience/Wind Soft Loop.wav | 24 | 2 | 44100 | -2.85 | -9.61 | 0.000183 |
| sfx.chime | Assets/QuietCamp/Audio/Generated/chime_soft.wav | 1.8 | 1 | 44100 | -1.92 | -16.31 | 0.018982 |
| ambience.crickets | Assets/QuietCamp/Audio/Generated/crickets_loop.wav | 8 | 1 | 44100 | 0 | -16.46 | 0.220184 |
| sfx.rustle | Assets/QuietCamp/Audio/Generated/leaf_rustle.wav | 0.7 | 1 | 44100 | -2.51 | -19.09 | 0.014954 |
| ambience.owl | Assets/QuietCamp/Audio/Generated/owl_hoot.wav | 2.8 | 1 | 44100 | -13.48 | -28.19 | 0 |
| sfx.twig | Assets/QuietCamp/Audio/Generated/twig_snap.wav | 0.3 | 1 | 44100 | 0 | -20.69 | 0.999969 |
| ambience.gust | Assets/QuietCamp/Audio/Generated/wind_gust.wav | 4 | 1 | 44100 | -5.07 | -21.56 | 0 |
| ambience.bird | Assets/ThirdParty/Audio/NaPH - RPG & Fantasy Sounds Bundle/Animals/Bird Chirp 06.wav | 1.4674 | 2 | 44100 | -10.34 | -40.12 | 0.000031 |
| ambience.fire | Assets/ThirdParty/Audio/NaPH - RPG & Fantasy Sounds Bundle/Environment/Fire Burning Loop 5.wav | 30 | 2 | 44100 | -13.55 | -40.77 | 0.010529 |
| ui.click | Assets/ThirdParty/Audio/UI Soundpack/WAV/Minimalist1.wav | 0.7563 | 2 | 44100 | -10.45 | -45.67 | 0.000031 |
| ui.back | Assets/ThirdParty/Audio/UI Soundpack/WAV/Minimalist2.wav | 0.7563 | 2 | 44100 | -10.65 | -46.5 | 0 |
| ui.select | Assets/ThirdParty/Audio/UI Soundpack/WAV/Minimalist3.wav | 0.7506 | 2 | 44100 | -12.85 | -45.25 | 0.000031 |
| placement.rotate | Assets/ThirdParty/Audio/UI Soundpack/WAV/Minimalist4.wav | 0.7506 | 2 | 44100 | -9.5 | -41.05 | 0.000031 |
| rule.invalid | Assets/ThirdParty/Audio/UI Soundpack/WAV/Minimalist5.wav | 0.7506 | 2 | 44100 | -10.12 | -42.28 | 0.000031 |
| level.complete | Assets/ThirdParty/Audio/UI Soundpack/WAV/Minimalist6.wav | 0.7506 | 2 | 44100 | -11.51 | -44.68 | 0.000031 |
| placement.commit | Assets/ThirdParty/Audio/UI Soundpack/WAV/Wood Block1.wav | 1.2857 | 2 | 44100 | -8.36 | -35.11 | 0.000031 |
| placement.undo | Assets/ThirdParty/Audio/UI Soundpack/WAV/Wood Block2.wav | 1.2857 | 2 | 44100 | -8.37 | -34.63 | 0 |
| Резерв Kenney | Assets/ThirdParty/KenneyUI/Sounds/click-a.ogg | — | — | — | — | — | — |
| Резерв Kenney | Assets/ThirdParty/KenneyUI/Sounds/click-b.ogg | — | — | — | — | — | — |
| Резерв Kenney | Assets/ThirdParty/KenneyUI/Sounds/switch-a.ogg | — | — | — | — | — | — |
| Резерв Kenney | Assets/ThirdParty/KenneyUI/Sounds/switch-b.ogg | — | — | — | — | — | — |
| Резерв Kenney | Assets/ThirdParty/KenneyUI/Sounds/tap-a.ogg | — | — | — | — | — | — |
| Резерв Kenney | Assets/ThirdParty/KenneyUI/Sounds/tap-b.ogg | — | — | — | — | — | — |

## Порядок виконання й остаточне приймання

1. Звір актуальний код/пакети, asset GUID і quality mapping. Збережи базові кадри меню й Camp; виміряй CPU/GPU/audio на доступному цільовому телефоні.
2. Узгодь єдиного автора атмосфери та JSON; перевір legacy day/evening, level overrides, reducedMotion/mute.
3. Доведи композицію шарів і palette матеріалів. Перевір, що наявні бірюзові крони не виглядають сторонніми поруч із sage/teal backdrop; коригуй leaf material окремо від кори, зберігай текстури/підматеріали та не перефарбовуй усе дерево одним кольором.
4. Реалізуй спільний вітер і rooted vertex/branch motion. Потім додай лише частинки, які справді потрібні.
5. Виправ loop/headroom і gain staging, підключи просторові джерела й phase scheduler. Прослухай суміш у навушниках і mono speaker.
6. Реалізуй camera/foliage transition у канонічному ScreenRouter: cover/readiness/recovery/lease/звук мають один lifecycle.
7. Додай помірний grading. Bloom/HDR/transient blur — тільки після вимірювання користі й витрат.
8. Заверши знімками реальної гри, відео переходу й коротким звітом: що змінилося, які перевірки виконані, що лишилося неперевіреним.

### Обов’язкові сценарії

- Boot→меню→Camp→наступний рівень→меню; повернення в Camp; restart; Back/repeated tap/drag під час переходу.
- Morning/noon/evening/night × Low/Balanced/High × reducedMotion. Mute не скидається. Існуючий save читається.
- Екрани16:9,19.5:9,20:9 та планшет4:3; notch/gesture inset. Усі крайні клітинки видно й можна натиснути; raycast дає ті самі логічні координати.
- Мінімум30 переходів: немає накопичення materials, RT, audio voices, scene roots, event subscriptions чи input locks.
- Повільне завантаження5с, штучна помилка assets/scene readiness, background/resume, resize. Немає білого/чорного flash, шматка напівготової сцени чи нескінченного busy.
- Вітер спокійний у steady state, коріння не ковзає, тіні узгоджені з деформацією. Leaf particles не перекривають правило/намет під пальцем.
- Окремо10 повторів кожної ambient loop; немає клацання, агресивного twig/chime чи відлуння на кожній кнопці.
- Compile, focused EditMode/PlayMode; для зміни композиції/старту — весь релевантний набір і smoke меню→Camp.
- На реальному слабкому Android і одному iPhone, якщо доступні:10–15хв, p50/p95 CPU/GPU, GC, texture/RT/audio memory, audio DSP, нагрів. За відсутності пристрою познач це явно; Editor fps не називай mobile fps.
- Перевір Vulkan/Metal/GLES, які реально ввімкнені в build; shader stripping, ASTC/fallback, відсутність magenta. Картинка Low має лишатися завершеною.
- Додаткову природу можна вимкнути, і правила гри/інпут/текст лишаються зрозумілими. Звук — підтримка відчуття, а не єдиний носій інформації.

### Очікуваний результат агента

Робоча інтеграція в реальній Unity-сцені, валідовані налаштування, цілісне керування життєвим циклом, необхідні регресійні тести, чотири реальні кадри фаз і відео переходу зі звуком. Не завершуй роботу звітом «усе готове», якщо існує тільки shader-файл без матеріалу/scene hookup або звук описаний, але не викликається. Не прирівнюй красиву генерацію картинки до перевіреного gameplay.
