# Промпт для ШІ: затишний перехід камерою крізь листя у QuietCamp

Це самодостатнє завдання агенту-виконавцю. Для розуміння вимог достатньо цього Markdown-файлу та доступу до цільового Unity-проєкту. Документ описує бажану реалізацію; наведені бюджети й художні значення потрібно перевірити, а не видавати за виміряний результат.

## 1. Що ти маєш реалізувати

Доопрацюй переходи між головним меню та gameplay-сценами QuietCamp так, щоб вони відчувалися як м’яке занурення в живий ліс.

Коли гравець обирає кемпінг, камера злегка наближається та плавно йде вниз до кущів. Близьке темне листя піднімається перед об’єктивом, стає більшим і м’якшим, закриває краєвид. Чується короткий природний шелест. Під листям непомітно завантажується нова сцена. Потім зелень відходить вниз і вбік, камера спокійно повертається у робоче положення, і гравець бачить наступну галявину.

Враження: «я пройшов крізь м’яку зелену завісу». Не атракціон, не падіння, не телепорт зі спалахом, не швидкий поворот голови. Рух має бути достатньо виразним, щоб помітити участь камери, і достатньо спокійним, щоб хотілося повторювати перехід.

Реалізуй завершений сценарій: camera motion, шари листя, імітацію/опційний ефект blur, звукові cues, приховане завантаження, готовність сцени, блокування інпуту, reduced motion, помилки та cleanup. Не зупиняйся на красивому overlay без руху камери або на одному shader без підключення.

## 2. На що спиратися в проєкті

Перевірено 30.09.2026. Перед змінами звір актуальний checkout: паралельні зміни зберігай. Ціль — quiet-camp/QuietCamp, Unity6000.6.2f1, URP17.6.0. Усі Assets/Packages шляхи нижче відносні до цього Unity-проєкту.

### Наявні точки інтеграції

| Файл / API | Фактична роль | Що робити |
|---|---|---|
| Assets/QuietCamp/Scripts/Presentation/ScreenRouter.cs | GoToMenu, GoToCamp, GoToNextCamp; busy; LoadSceneAsync | Залишити єдиним автором навігації |
| Assets/QuietCamp/Scripts/Presentation/FoliageDiveTransition.cs | Уже існує persistent overlay, стани, рух камери, листя, rustle, recovery | Доопрацювати його; не створювати другий transition component |
| Assets/QuietCamp/Scripts/Presentation/World/CameraFitter.cs | Ортографічна камера, повний camera.rect, fit за BoardViewport | Зберегти канонічну базову позу, додавати лише косметичний offset |
| Assets/QuietCamp/Scripts/Presentation/World/CampAtmosphere.cs | Фон, rear/near шари; LateUpdate та RefreshLayout | Узгодити ownership камери/near, особливо resize під час переходу |
| Assets/QuietCamp/Scripts/Presentation/World/CampSceneHost.cs | Побудова gameplay й застосування фази | Надати явний сигнал готовності world/layout/атмосфери |
| GameServices, ContextStack, InputPolicy | Сервіси й обмеження інпуту | Використовувати штатні механізми, не тільки UI raycast blocker |
| Packages/com.kruty1918.audio/Runtime/AudioService.cs | Пул, keys, buses, handles | Звуки переходу запускати й прибирати через нього |
| Assets/QuietCamp/Tests/PlayMode/SceneTransitionPlayModeTests.cs | Існуючі перевірки переходів | Звірити, оновити під фінальну поведінку, додати потрібні regression cases |

Поточний ScreenRouter уже використовує FoliageDiveTransition в усіх трьох напрямках. Не виконувати застарілу пораду «спочатку заміни Doors/Iris/Curtain», якщо вони вже замінені.

Поточна камера: orthographic, Euler(55,225,0), distance20, near0.1/far60; size і position підлаштовує CameraFitter. Не переводити її на perspective. Зберегти інпут і світові координати клітинок.

### Конкретні місця для перевірки в поточній реалізації

1. Після LoadSceneAsync зараз стоять два Task.Yield. Це не доводить, що layout/фон готові або кадр реально намальований. Потрібний явний readiness-контракт.
2. У FoliageDiveTransition перевір, чи reveal зменшує той самий normalized progress, який читає LayoutLeaves. Недостатньо змінити лише alpha cover або позицію камери.
3. CoverAsync після recovery не має знову переводити стан у CoveredLoading; очікування не повинні воскресати після скасування.
4. WaitIdle із лімітом кількості циклів Task.Yield не є надійним wall-clock timeout. Використовуй явний cancellation/lifecycle й elapsed time.
5. Перевір, чи Recovery не відкриває напівзавантажену сцену. Finish має прибирати visual state тільки тоді, коли є придатний екран для показу.
6. Перевір базову позу, яку зберігає CaptureCamera, при resize та повторному fit; не відновлювати застарілий розмір.
7. CampAtmosphere має camera-relative quads. Вони не отримають потрібний паралакс автоматично тільки від руху camera Transform.
8. Всі зауваження вище — завдання на перевірку поведінки, а не дозвіл переписати весь bootstrap.

## 3. Межі застосування

| Дія гравця | Анімація |
|---|---|
| Меню → почати/продовжити кемпінг | Повний м’який foliage dive |
| Поточний → наступний рівень | Та сама візуальна мова, нова сцена відкривається з власною фазою доби |
| Gameplay → меню | Та сама зелена завіса; нижня інтенсивність звуку допустима |
| Перший показ гри | Лише коротке розкриття з готового cover, без удаваного занурення з порожньої сцени |
| Пауза, налаштування, локальна підказка | Локальне відкриття UI; камера не занурюється |
| Restart того самого рівня | Коротка версія за тим самим lifecycle або штатне локальне скидання; не запускати два сценарії |
| Reduced motion | Нерухома камера, без летючого листя/blur, короткий dissolve |

Не роби занурення на кожен клік, вибір намету чи невдале розміщення. Цей перехід має означати зміну місця, а не будь-яку UI-дію.

## 4. Камера: точний контракт

На старті збережи актуальні baseline position, rotation, orthographicSize, camera.rect та ідентичність камери. Окремо збережи UI/layer стан, який ти тимчасово змінюєш.

Нехай:
- S — baseline orthographicSize;
- H=2×S — видима висота world у повноекранній ortho-камері;
- U — camera up, збережений із baseline rotation;
- p — normalized cover progress0..1;
- E(p)=p²×(3−2p) — початкова smoothstep-крива.

Базова анімація:
- cameraPosition = baselinePosition − U×H×0.16×E(p);
- orthographicSize = S×lerp(1,0.94,E(p));
- rotation = baselineRotation; optional pitch≤2° лише після A/B, базово0;
- roll=0; camera shake=0; bounce/elastic=0.

«Вниз» — **screen-down уздовж −baseline camera up**, а не globalY. При цьому world на екрані природно зміщується вгору. Не пересувай логічне поле, не змінюй координати наметів і не переводь камера/інпут у другу систему координат.

Рух уперед уздовж forward сам по собі не дає перспективного збільшення в orthographic camera. Відчуття наближення створюють невелике зменшення orthographicSize та сильніше збільшення близького листя.

На reveal використовуй baseline НОВОЇ камери:
- початкова позиція baselinePosition − U×H×0.10;
- початковий size≈S×0.96;
- далі плавне повернення до точного baseline за300–420мс;
- після завершення явно встанови baseline, щоб не накопичувати похибку між30 переходами.

Не lerp поточну позицію в саму себе щокадру: результат не має залежати від fps. Криві обчислюються від зафіксованих початкових значень та normalized elapsed time. Пауза гри з Time.timeScale=0 не повинна блокувати перехід із меню; використай unscaled animation time. При application pause timeline зупиняється; після resume не додавати час, проведений у background, одним великим стрибком.

### Спільна робота з fit і шарами

Один власник пише camera Transform у кожний момент. Під час переходу звичайний CameraFitter/CampAtmosphere не повинен щокадру перезаписувати анімацію. Використай явний composition: latest fitted baseline + transition offset. Resize може оновити baseline, але не запустити другий tween.

Не зберігай стару baseline з меню для нової Camp-камери. При зміні сцени старий Transform може бути destroyed — не звертайся до нього після load.

## 5. Режисура листя та глибини

Використовуй три візуальні плани:
1. Далекий ліс: майже нерухомий, контраст стриманий.
2. Реальне поле/декор: рух від камери.
3. Близькі кущі/окремі листки перед об’єктивом: найбільший рух, масштаб і м’якість.

Додатковий паралакс понад рух камери: far≤0.1×, middle≤0.3×, near1×. Не додавати однаковий offset усім шарам поверх уже застосованої camera translation — це може подвоїти рух.

### Розміщення листя

Головне джерело руху — кущі знизу. Верхні кути можуть підтримувати рамку, але не перетворюй перехід на чотири симетричні пелюстки, які змикаються до центру.

Для Balanced:
- широкий лист/гілка A входить із нижнього лівого кута;
- B із нижнього правого входить на40–70мс пізніше;
- тонший лист C проходить по лівій або правій периферії;
- D — останній близький м’який край, який приховує зміну cover.

Стартовий розмір великих силуетів≈35–55% ширини екрана; максимум у фазі перекриття≈55–80%. Це близькі гілки, не багато дрібних particles. На планшеті рахуй від короткої сторони й перевіряй покриття кутів; не розтягуй квадратний спрайт у вузький прямокутник.

Near масштаб1→1.15–1.25, рух переважно знизу вгору. Поворот кожного силуету в межах±6–10°, без повних обертів. Перед reveal листя плавно йде вниз/за межі екрана. Окремі blade edges можуть відставати на40–80мс. Не використовуй random щокадру; схема стабільна й перевірювана.

Не малювати траєкторії перед очима гравця як конфеті. Листя не телепортується, не змінює форму кадрами atlas і не торкається HUD. Зовнішні краї sprites виходять за screen rect на4–8%, щоб не показувати обрізаний прямокутник.

### Графіка й матеріали

Наявні файли:
- Assets/QuietCamp/Resources/QuietCamp/Atmosphere/Textures/foreground.png — близький темний шар із alpha та м’якими краями;
- Assets/QuietCamp/Resources/QuietCamp/Atmosphere/Textures/leaves.png — atlas, який поточний transition завантажує через Resources.LoadAll<Sprite>;
- оригінал atlas також збережений у Design/Atmosphere/assets/leaves.png від кореня репозиторію.

Перевір фактичні sub-assets і Sprite Mode Multiple: сам PNG не гарантує, що LoadAll<Sprite> повертає чотири спрайти. Source1254×1254,2×2 клітинки627×627. Верхній ряд у Unity Sprite Rect має y627, нижній y0. Pivot center. Кожній формі дати стабільне ім’я/ID; не залежати від неперевіреного порядку LoadAll.

Screen-space textures: sRGB, Clamp, Bilinear, Read/Write Off. Перевір padding/atlas bleed/alpha fringe на світлій і нічній сцені. При потребі м’якого leaf asset використовуй готовий alpha sprite; не перетворюй чорне тло на alpha грубим color key.

Low/Balanced не потребують шейдера refraction/distortion. Базове змішування straight alpha: SrcAlpha/OneMinusSrcAlpha. Якщо pipeline використовує premultiplied alpha — це окремо узгоджений шлях, множити RGB на alpha рівно один раз.

У звичайному gameplay protected rect приховує foreground над полем. Transition-leaves — окремий короткоживучий overlay, який під input lock МОЖЕ закрити весь екран. Не вимикай назавжди захисну маску gameplay-near заради переходу.

## 6. Таймінг звичайного переходу

Базово≈1.0–1.1с руху плюс фактичне очікування готовності сцени. Завантаження не має штучно затримуватися для демонстрації ефекту. Нижче художні стартові значення, не жорстка вимога точності до мілісекунди.

| Від початку | Камера | Листя / cover | UI / звук |
|---|---|---|---|
| 0–80мс | Зберегти baseline; почати дуже м’яко | Краєвид ще відкритий | Один click, якщо він уже не зіграв у кнопці; cancel drag; input lock |
| 80–220мс | Offset0→−0.05H; size1→.985 | Нижні кущі наближаються | Другорядний UI opacity1→0 за120мс |
| 220–360мс | Offset−.05H→−.12H; size.985→.955 | Великі листки проходять перед лінзою, збільшуються | Основний rustle cue біля260мс |
| 360–440мс | До−.16H і size.94 | Листя заповнює кадр; opaque cover доводиться до1 | Вхідний звук спокійно дограє |
| 440–520мс | Поза утримується | Alpha cover точно1; кадр повністю закритий | Зафіксувати covered frame; load можна продовжувати |
| covered→ready | Старий world прихований | Непрозорий темний лісовий колір; майже нерухомі листки | Без loop whoosh; контрольований crossfade ambience |
| ready+0–80мс | Зберегти новий fit; задати reveal offset | Cover ще непрозорий | Стабілізувати world/layout/listener |
| ready+80–380мс | Плавно повернути новий baseline | Листя відходить вниз/вбік; cover1→0 | Тихіший exit rustle, один раз |
| ready+380–500мс | Точний fit | Звичайний gameplay-near | Плавно повернути потрібний UI; віддати input |

Розділи cover progress, reveal progress і camera blend явно. Не лишати leaf progress=1, коли камера вже повернулася. На reveal всі рухомі системи читають узгоджену timeline, а не незалежні корутини зі схожою тривалістю.

Тривалість у конфігурації є джерелом істини. Зміна coverDuration повинна пересувати й cues/leaf phases через normalized markers, не ламати абсолютні таймери.

## 7. Opaque cover та порядок відмальовування

Прозорі PNG мають отвори й не гарантують прихованої зміни сцени. Обов’язковий окремий **суцільний непрозорий** cover на весь framebuffer:
- базовий колір #173638;
- без текстури з дірками, без залежності від safe area;
- покриває notch/gesture inset/letterbox;
- transition Canvas вище gameplay і HUD; листки вище cover;
- recovery UI, якщо потрібне, має лишатися доступним поверх cover.

Загальний CanvasGroup активного переходу тримай alpha1; анімуй alpha самого cover та окремих leaf groups. Не перемножуй випадково alpha двох батьківських group так, що «закритий» кадр просвічує.

Covered — не просто значення enum. Перед load/activation/демонтажем старої сцени перевір фактичне покриття екрана, alpha1 і хоча б одну гарантовану точку рендеру закритого кадру. Не блокуй main thread до того, як cover встиг показатися.

Для menu/day→night cover може м’яко змінювати tint до темнішого #132D38 лише після повного закриття. Для morning/noon — стриманий #203D36. Не анімувати глобальний tint усієї гри під відкритим кадром. Суцільна база має лишатися темною, без чорного flash.

## 8. Завантаження, readiness й відновлення

Єдина state machine:
Idle → Covering → CoveredLoading → Preparing → Revealing → Idle.
Recovery — явний аварійний шлях із контрольованим придатним екраном.

### Readiness нової сцени

AsyncOperation.isDone означає завершення scene operation, але не підтверджує готовність усіх application-level систем. Reveal дозволено після:
1. scene host і сервісні bindings готові;
2. рівень прочитаний/валідований, world створений;
3. HUD layout застосований;
4. CameraFitter встановив актуальний fit;
5. відповідні backdrop/phase/light застосовані або активовано придатний fallback;
6. один AudioListener, потрібний ambience owner, немає дубльованих loops;
7. новий world справді відрендерений за cover хоча б один раз.

Використай явний сигнал готовності scene host і одноразовий render-completion hook для потрібної камери. Відписка в усіх гілках. Task.Yield, довільний WaitForSeconds чи кількість кадрів не є заміною цьому контракту.

У звичайному runtime не покладатися сліпо на WaitForEndOfFrame як універсальний механізм для batchmode tests; обрати механізм, який перевіряється у тестовому середовищі й не вимагає активного Editor Game View.

Не переводити весь проєкт на additive loading лише заради цього ефекту. Якщо використовується allowSceneActivation=false, окремо перевірити залежності операцій: не чекати готовності host, який ще не міг активуватися.

### Повільне завантаження й помилки

- Понад1.5с очікування: маленька локалізована підказка «Стежка вже поруч…». Не показувати її на кожному звичайному переході.
- Жодного вигаданого відсотка прогресу.
- Після≈15с: recovery UI із зрозумілим станом; не оголошувати операцію завершеною лише через timeout.
- Retry недоступний, поки попередній фізичний load може небезпечно завершитися й перезаписати сцену. Спершу серіалізуй/заверши активну операцію, потім починай нову.
- Unity scene load не має універсального миттєвого cancellation. Generation token відкидає застарілі callbacks, але сам по собі не скасовує завантаження.
- Назад веде до відомої придатної сцени; якщо стара scene вже знищена, не викликай Reveal на її камері.
- Якщо помилка тільки в blur/leaf asset, дай дешевий opaque dissolve fallback; не блокуй навігацію через декоративний asset.
- Якщо gameplay level невалідний, не показуй порожнє поле як успішний результат.
- Recovery UI не блокується тим самим modal gate, який заблокував гру.

У finally звільняються саме власні lease/handles/subscriptions. Асинхронні loops завершуються при dispose/cancel і не чекають Update знищеного GameObject. Destroy компонента посеред переходу не залишає forever-awaited Task.

## 9. Blur з урахуванням телефона

Базове відчуття blur створює близька оптично м’яка графіка, а не обов’язковий повноекранний постефект.

| Якість | Графіка | Реальний blur |
|---|---|---|
| Low | Два великі leaf silhouettes + near + opaque cover | Вимкнений |
| Balanced | Чотири silhouettes, різна глибина/масштаб, м’які alpha edges | Вимкнений за замовчуванням |
| High | До шести silhouettes, той самий базовий сценарій | Лише опційний короткий directional blur world |

Не вмикати постійні Motion Blur, DOF, temporal accumulation, motion vectors, chromatic aberration, lens distortion чи camera shake.

### High, якщо A/B виправдовує вартість

- Активність лише приблизно220–440мс на вході й коротко на початку reveal.
- Downsample width/height до1/4: це1/16 pixels вихідного кадру, а не1/4 загальної площі.
- Два проходи максимум; орієнтир5 taps кожний, нормалізовані ваги.
- Переважний напрям уздовж руху кадру; не перетворити його на довгі vertical streaks.
- Видимий радіус world blur≈4–8 logical px на reference-width1080. Правильно перевести в UV/texels quarter-resolution target; не застосувати downsample scale двічі.
- Сила0→max→0, не імпульс одразу на першому tap.
- Розмивається world color. HUD/підказка loading/recovery текст не проходять через blur.
- Не розмивати near foliage додатковими full-screen passes, якщо воно вже м’яке.

URP17: RenderGraph із явними read/write texture dependencies; не семплити й не писати той самий target. Backbuffer не вважати гарантовано sampleable. Якщо потрібний intermediate/copy, він має бути валідний у конкретній renderer-конфігурації. Звір API з установленим URP17.6, не копіюй сигнатури з іншої версії.

Не використовувати legacy CommandBuffer.Blit і не вимикати RenderGraph лише заради старого прикладу. У steady gameplay feature не має виконувати непотрібний pass чи примушувати всі камери постійно робити color copy. Врахувати MSAA resolve, HDR format, dynamic resolution, orientation; blur не вимагає depth.

Усі temporary resources мають чіткий lifecycle. Persistent RTHandle release на dispose; frame-local RenderGraph ресурси живуть за правилами графа, не кешуються як довготривалі handles. Не читати destroyed cameraColor після scene swap.

Якщо відмінність від baked-soft foliage невиразна або перший перехід hitch-ить, High використовує Balanced fallback. Це завершений варіант, а не візуально зламаний «режим економії».

## 10. Звук: тихо, причинно, синхронно

Використай наявний AudioService та source keys. Збережи користувацькі Ui/Sfx/Ambience/Master налаштування; mute завжди має пріоритет.

| Момент | Джерело | Початкове налаштування |
|---|---|---|
| Натискання старту | ui.click | Лише якщо сама кнопка ще не зіграла feedback; один click |
| Вхід у перше велике листя,≈260мс | sfx.rustle | Основний cue; gainScale≈.20–.35 від чинного definition, потім слухове tuning |
| Повністю закритий кадр | Немає нового accent | Не грати удар, bass drop чи «портал» |
| Вихід із листя, ready+80..140мс | sfx.rustle | На3–6dB тихіше входу; для gainScale це множник≈.5–.71 |
| Тривале loading | Базовий ambience | Без повторного rustle/loop whoosh |
| Помилка load | М’який UI feedback за потреби | Один раз разом із видимим повідомленням |

Ресурси:
- sfx.rustle → Assets/QuietCamp/Audio/Generated/leaf_rustle.wav,≈0.7с, mono;
- ambience.gust → Assets/QuietCamp/Audio/Generated/wind_gust.wav,≈4с, mono;
- ui.click → наявний Minimalist1 у AudioCatalog.

Wind gust не обрізати до150мс і не програвати4с щоразу, коли весь рух триває≈1с. Він належить природному пориву; transition може узгодити з ним подію, але не створювати подвійну хвилю.

Для «leaf at lens» —2D Sfx із легкою панорамою±.10..15 максимум. Панорама відповідає фактичному боку leaf crossing; не випадкове бігання по навушниках. Doppler0. Echo/reverb для близького transition rustle вимкнути. Лісова глибина походить із фонової просторової сцени, а не з кімнатного ехо на листку.

AudioSource/Listener не повинні різко міняти дистанцію через косметичний plunge. Перевір єдиний listener та стабільну позицію слухача або відповідний adapter. Не змінювати весь gameplay3D баланс під час короткого screen transition.

Наявний PlayAt може примусово ставити spatialBlend1: для transition, який задуманий як2D, не викликати його заради самого факту руху листка. Використати правильні play options/канонічний service; не діставати pooled source й запускати його вручну поза обліком.

### Cues та мікс

- Cues прив’язані до перетину normalized timeline marker. Flag «зіграно» належить одному transition generation.
- Frame hitch не має запускати кілька пропущених cues пачкою.
- Зміна тривалості/skip/reducedMotion не створює дубліката.
- Не запускати звук у кожному Update, LayoutLeaves або SetProgress.
- Exit cue — окремий handle; ранній recovery не грає його як «успішний вихід».
- За скасування короткий акуратний fade20–50мс, якщо підтримує сервіс; не зупиняти чужі ambience voices через StopAll.
- Дві leaf voices максимум одночасно; жодних нових AudioSource на кожен tap.
- Вхідний/вихідний clip не підвищувати до пікового перевантаження. Перевір слухову гучність на тихому телефоні; наведені gainScale не є нормалізованими LUFS.

Ambient dip під час входу максимум≈−2dB, attack120мс, release400–800мс. Застосувати через scoped multiplier/duck API, а не перезапис користувацького bus volume. При recovery відновити автоматичний множник1, зберігши user mute/volume.

Коли день переходить у ніч, новий ambience owner плавно змінює bird/cricket/fire weights за0.8–1.5с. Не запускати всі природні events на першому кадрі reveal. Той самий постійний wind дозволено лишити без restart. Якщо phase scheduler уже володіє crossfade, transition не запускає другий.

## 11. Добові варіанти

Доба змінює тональність, але не механіку/тривалість інпут-блокування:
- morning: м’які sage highlights, без жовтого спалаху;
- noon: листя трохи світліше, темна підкладка стримана;
- evening: невеликі теплі краї, глибокі прохолодні тіні;
- night: приглушений teal/blue, відсутність неонових світних листків.

Перед cover видно палітру старої сцени; під opaque cover переходь до палітри нової; reveal показує вже узгоджену нову фазу. Не фарбуй листя на яскравий денний колір за мілісекунду до нічного reveal. Не запускай глобальну зміну доби як побічний ефект будь-якої навігації.

## 12. Інпут, reduced motion, доступність

Від старту Covering і до завершення Revealing заблокувати gameplay taps/drags, команди клавіатури та звичайні navigation actions. Raycast-blocking Canvas — лише один шар захисту; потрібен канонічний InputPolicy/context gate.

Active drag скасувати без випадкового commit або втрати вже розміщеного намету. Перекриття UI не може саме по собі завершувати gesture у сторонній клітинці.

Repeated tap ігнорується без черги з10 завантажень. Для системного Back задати одну явну політику: відкласти один intent до безпечної точки або відкрити recovery navigation, якщо це доречно. Back не запускає другий ScreenRouter. Recovery-кнопки мають whitelist у gate.

Reduced motion:
- camera Transform/size не анімуються;
- немає flying leaves, parallax, tilt, blur;
- cover140мс → actual readiness wait → reveal180мс;
- допускається статичний foliage border без руху;
- один тихий click або зовсім без rustle; mute завжди поважати;
- той самий readiness/error/input lifecycle, без окремої ненадійної гілки завантаження.

У всіх режимах немає спалахів/стробу. Loading текст локалізований, читабельний і поза blur. Safe area застосовується до тексту/кнопок, а сам cover покриває весь екран.

## 13. Пауза, resize, lifecycle

При background timeline не накопичує elapsed time; анімаційні cues не доганяють пропущене при resume. Async load може завершитися незалежно — після повернення перевір фактичний стан і readiness.

При resize/orientation:
- негайно онови fullscreen cover;
- перерахуй leaf paths у нормалізованих координатах;
- онови fitted baseline і продовж на тому самому normalized progress;
- protected rect gameplay-near відновлюється після переходу;
- тимчасові RT відповідають новому descriptor.

Ідемпотентне завершення: повторний cleanup/recover нічого не ламає. Busy, context gate, UI alpha, camera pose, sound handles, subscriptions і temporary materials звільняються в усіх гілках. Після зміни сцени не відновлювати значення на чужій новій камері випадково через старий reference.

Якщо persistent FoliageDiveTransition переживає scene load, його GameServices reference має залишатися валідним. При повному shutdown/bootstrap reset старі tasks/handles скасовані й component не продовжує Update з disposed services. Новий дубль Ensure не повинен з’являтися після кожної сцени.

## 14. Бюджети та конфігурація

Заведи один JSON-контракт transition або розшир існуючий придатний JSON. Не робити другий конфіг із тими самими значеннями в Inspector і коді.

Поля: coverDuration, coveredHold, revealDuration, cameraDepthFraction, cameraSizeMultiplier, revealDepthFraction, reducedIn/out, slowHintDelay, recoveryThreshold, cueMarkers, вход/вихід gain, phase leaf tints, quality leaf counts, optional blur parameters.

Validate → resolve → freeze. Межі: durations додатні; depth0..0.2H; size.9..1; leaf counts0..6; blur radius0..8 logical px; normalized cue markers0..1. Не використовувати невідомі CLR type names у JSON.

Орієнтири:
- Low: до2 leaf sprites + один cover, blur0;
- Balanced: до4 leaf sprites, blur0;
- High: до6, optional transient blur;
- два короткі Sfx voices;
- жодного Instantiate/Destroy матеріалів/спрайтів щокадру;
- steady state без allocation/render pass від transition;
- допуск CPU overhead≈0.3мс / GPU≈1мс на погодженому середньому пристрої як ціль, не обіцянка;
- оцінити не тільки number of particles, а прозоре покриття/overdraw/color copies.

Візуальні leaf objects можна створити один раз і перевикористовувати. Перед першим видимим dive підготувати потрібні assets/shader variants, щоб не отримати first-use hitch. Це не привід прогрівати всі варіанти всіх шейдерів проєкту.

## 15. Порядок реалізації

1. Перевір поточний ScreenRouter/FoliageDiveTransition й залежності. Зафіксуй фактичну поведінку коротким відео.
2. Спершу впорядкуй states/gate/readiness/recovery, без зміни художніх параметрів.
3. Реалізуй baseline+offset camera contract та синхронний reveal progress.
4. Доведи композицію листя: нижня зелена завіса, масштаб, stagger, відсутність прямокутників.
5. Додай два синхронні тихі sound cues та правильний lifecycle/duck.
6. Перевір Low/Balanced і reduced motion; лише потім розглядай optional High blur.
7. Пройди регресійні сценарії; поправ докази/опис під фактичний результат.

Не змінюй правила рівнів, сейви, якість тексту, gameplay-camera projection або сторонні пакети без конкретної потреби. Якщо змінюєш канонічний package API — зберегти сумісність і перевірити його інших споживачів.

## 16. Критерії приймання

| Перевірка | Очікуваний результат |
|---|---|
| Меню→Camp, Camp→меню, next level | Є виразний м’який рух камери й foliage; одна навігаційна операція |
| Scrub відео по кадрах | Немає scene flash, прямокутного краю PNG, стрибка alpha або листків, що зависли після reveal |
| Slow load5с | Opaque cover утримується, підказка з’являється після1.5с, немає loop rustle |
| Missing leaf/blur asset | Контрольований дешевий dissolve; навігація працює |
| Scene/level failure | Придатний recovery screen і доступні дії, немає нескінченного lock |
| Repeated tap/Back/drag | Немає подвійного load, випадкового placement чи накопиченої черги |
| Ніч→ранок | Палітра/звук змінюються під cover; reveal уже узгоджений |
| Reduced motion | Камера нерухома, blur0, flying leaves0 |
| Mute / тихий mono speaker | Mute не скидається; cues не оглушують і не потрібні для розуміння стану |
| Pause/resume/resize | Немає програвання cues пачкою, прогалин cover чи відновлення застарілого fit |
|30 послідовних переходів | Після Idle камера в точному актуальному fit; немає росту RT/materials/voices/subscriptions |
| Low телефон | Стабільна анімація без дорогого постійного postprocessing |
| Compile + focused EditMode/PlayMode | Перевірені states, cleanup, input, readiness; немає нових Console errors |
| Vulkan/Metal/GLES згідно build | Немає magenta/unsupported shaders, коректний alpha й color target |

Фінальна здача агентом: робочі зміни, конфігурація, релевантні тести, коротке відео переходу зі звуком і варіант reduced motion. Окремо назвати реально перевірені devices/quality tiers. Editor preview не вважати доказом mobile fps. Не стверджувати, що blur реалізований, якщо працює тільки baked-soft fallback; чесно назвати обраний спосіб.

## 17. Перевірені технічні джерела

Художні криві й цифри вище — запропонований дизайн QuietCamp. Технічні обмеження звірені з офіційною документацією; точні API перевіряй у встановленому URP17.6:

- [Unity: Render Graph у URP](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/render-graph-introduction.html) — ресурси/залежності проходів та їх оптимізація.
- [Unity: Blit у URP](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/customize/blit-overview.html) — сумісні шляхи копіювання, обмеження legacy blit.
- [Unity: SceneManager.LoadSceneAsync](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/SceneManagement.SceneManager.LoadSceneAsync.html) — асинхронна scene operation. Готовність ігрового host/layout є окремим контрактом цього завдання.

Цей файл — промпт для майбутнього виконання. Саме створення документа не означає, що перелічені доробки вже внесені в Unity.
