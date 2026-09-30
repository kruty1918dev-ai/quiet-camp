# QuietCamp — точне завдання на мінімалістичний UI та занурення

Дата аналізу: 30.09.2026. Цей файл можна передати агенту-виконавцю цілком. Поточне завдання створило специфікацію та інтерактивну схему; виробничий код ще не змінено.

Перегляд: [інтерактивна схема](interaction-preview.html) · [шість ключових станів одним зображенням](UI-STATES.jpg). Для початку реалізації передай агенту цей файл і доручи виконати розділи 0–19; розділ 20 фіксує межі вже виконаного аналізу.

## 0. Завдання виконавцю та критерій результату

Ти працюєш у `quiet-camp/QuietCamp`, Unity 6, URP, C#, uGUI, TextMeshPro. Workspace розмови може вказувати на Moyva — це не ціль цього завдання. Спочатку перевір абсолютний шлях. Реалізуй описаний нижче дизайн у реальних MainMenu і Camp, збережи правила головоломки, збереження, навігацію й локалізацію. Заверши роботу перевіркою реального білда/PlayMode, а не лише зміною префабів або HTML.

Гравець має бачити лісову галявину, відчувати тепле світло, помічати власні дії та розуміти задачу без постійної стіни кнопок. Основна взаємодія — з наметами й полем. Інтерфейс тихо пояснює поточний контекст, а після завершення дії звільняє екран. Прихований UI означає приховані другорядні панелі, а не невідомі правила, невидимі кнопки чи керування, яке можна знайти лише випадково.

Обов’язковий результат: єдина візуальна мова всіх екранів; справжня контекстна поведінка; читабельний світ; контрольована анімація; жодних регресій вводу/правил. Заборонено вважати завдання виконаним після recolor поточного HUD.

Пріоритети при конфліктах: коректність правил і збережень → передбачуваний ввід → доступність інформації → композиція світу → декоративні ефекти. В межах презентації цей документ замінює старий варіант чотирьох великих кнопок з `Design/MainMenu/MAIN-MENU-PLAN-UA.md`. Попередні проморендери — референс атмосфери, не специфікація геометрії рівня чи доказ наявних функцій.

## 1. Що вже перевірено в репозиторії

Перед змінами перевір актуальні реалізації перелічених методів; код може змінитися після дати аналізу. Не читай усі файли проєкту. У QuietCamp не знайдено власного CODEMAP/AGENTS на момент аналізу; якщо вони з’явилися, врахуй актуальні інструкції.

| Файл / метод | Поточний факт | Необхідна зміна |
|---|---|---|
| `Scripts/Presentation/UI/CampHud.cs`: BuildHeader/BuildRulesRow/BuildTray/BuildActions/BuildNav | Одночасно будуються header, усі rule chips, картки, чотири дії та нижня навігація | Замінити контекстною моделлю з розділів 5–10 |
| CampHud.WishKey | Повертає shade, інакше quiet, інакше path; не показує весь набір побажань | Повний список умов гостя; friends із Level.friends; прохід як загальна умова |
| CampHud.RefreshCards | placed і selected зведені до кольору; placed має пріоритет у виразі | Окремі стани selected/placed/needs-attention; позначення не лише кольором |
| CampHud.RefreshChips | Глобальна помилка правила фарбує його chip для всіх | Пояснювати проблему конкретного гостя й клітин, не створювати відчуття «усе неправильно» |
| `World/PlacementController.cs`: OnPress/OnRelease | Натискання поставленого намету відразу створює preview; release викликає Commit | Розділити inspection, armed move та drag; tap inspection не створює команду |
| PlacementController.ReadPointer | TouchPhase.Canceled входить до загальної гілки touch; окрема семантика cancel не виражена | Явно розрізняти release/cancel; скасування ніколи не комітить |
| `World/CampSceneHost.cs`: ShowAreaOverlay/ShowMoveOverlay | Методи-заглушки | Реальні area/ghost overlays із canonical HintService |
| CampHud.ShowCompletion + CampSceneHost.OnLevelCompleted | Модальне завершення відкривається від події; host зберігає альбом і змінює світло | Дати спочатку побачити результат, потім немодальна компактна картка; збереження не відкладати до анімації |
| `UI/QcUi.cs` | Green кнопка бере tint=white і насичений зелений PNG; MinTouch=88 canvas units із коментарем про dp | Явні semantic styles та перевірка фізичного touch target; коментар не є доказом 48 dp |
| `UI/SettingsPanel.cs` | Є textScale 0.85–1.3, reducedMotion, calmMode, highContrast, haptics | Не дублювати налаштування; під’єднати їх до нової презентації; обґрунтовано розширити textScale до 1.5 |
| `UI/MenuScreens.cs`: BuildMain | Центрована вертикальна колонка чотирьох однаково великих дій | Одна головна дія, два легкі другорядні маршрути, settings у куті |
| `World/MenuSceneHost.cs`: BuildDiorama | Декорація меню залежить від witness QC_TEST | Окрема авторська композиція меню або готовий статичний фон; без залежності від тестової задачі |
| `World/CameraFitter.cs`: Fit | camera.rect прив’язується до BoardViewport; fit включає SceneryMargin | Повноекранний світ; playable bounds вписуються в доступну область, декоративні дерева не стискають поле |
| `UI/ToastPresenter.cs` | Toast має фіксовані offsets і власну DOTween sequence | Спільний message slot, motion settings, довгі тексти й stable priority |
| `Presentation/ScreenRouter.cs` | Уже є busy/transition lease та Doors/Iris/Curtain | Перевикористати навігацію/блокування; уніфікувати лише стиль переходів |
| `Application/CampSession.cs` | Preview/TryCommit/Undo/Redo; completion тільки через Check | Залишається єдиним шляхом зміни ігрового стану |

Усі шляхи Scripts вище відносні до `Assets/QuietCamp/`. Переглянуто також Screenshots/04_camp_day.png, 07_camp_evening.png, 03_menu_levels.png: вони демонструють перевантаження, але можуть бути старішими за код. Не стверджуй, що старий кадр показує актуальний runtime без повторного запуску.

## 2. Візуальна ідея та межі

Назва напряму: «Тиха галявина». Low-poly світ, небагато кремових поверхонь, темний лісовий текст, теплі акценти вогню. Відчуття маленького доглянутого табору. UI схожий на коротку записку господаря кемпінгу, а не на панель керування редактора.

Основні референси, від кореня репозиторію quiet-camp:

- `Design/MainMenu/assets/menu-background-phone.png`: світло, глибина, колір наметів і композиційна тиша.
- `Design/MainMenu/assets/menu-background-tablet.png`: відступи й просторіший фон.
- `Design/StoreGallery-UA/masters/01-find-your-calm.png`: загальна теплота.
- `Design/StoreGallery-UA/masters/03-shade-and-care.png`: ніжна тінь та фокус на одному місці.
- `Design/StoreGallery-UA/masters/05-a-warm-evening.png`: читабельний вечір.
- `Design/AppIcon/quietcamp-icon-master.png`: впізнаваність червоного намету; не вставляти всю іконку в кожну панель.

НЕ копіюй рекламні заголовки в gameplay. НЕ замінюй інтерактивне поле PNG-рекламою. НЕ повторюй висячий намет, довільні сітки або світлові плями рендера як нову механіку. Для gameplay використовуй реальні моделі, canonical level data та той самий projection/raycast path.

Меню може тимчасово використовувати готову ілюстрацію; остаточний найкращий варіант — жива авторська діорама з наявних моделей. Статичний режим прийнятний, якщо його чесно позначено у звіті, а під PNG не створюється прихована 3D-діорама. Gameplay завжди 3D. Ніяких необхідних дій, доступних тільки натисканням на декоративне багаття, дерево чи схований предмет.

## 3. Дизайн-токени: фіксовані стартові значення

Це власні вимоги дизайну, не налаштування, які вже існують. Усі розміри нижче — логічний макет шириною 390 одиниць. Не називай ці одиниці фізичними dp без вимірювання на пристрої. З базовим Canvas 1080×1920 переведи значення через фактичний Canvas.scaleFactor/ширину safe-area RectTransform; не множ усе сліпо на 3 на кожному екрані.

| Токен | Значення / використання |
|---|---|
| Ink / основна CTA | `#243E35` |
| Cream / поверхня | `#F3EFE3`, alpha 0.98; high contrast 1.00 |
| Muted ink | `#526756`; лише другорядний текст на Cream |
| Surface border | `#D7DCC9`, 1 логічна одиниця |
| Warm accent | `#E9B56D`; підсвічування дії, не дрібний текст на Cream |
| Attention ink | `#87412D` на `#FFF1DC`; спокійна помилка без червоного спалаху |
| Focus ring | `#243E35` на світлому; Cream на темному, 2 одиниці + 2 offset |
| Scrim | `#172C24`, alpha 0.28; modal максимум 0.40, без дорогого blur |
| Відступи | 4 / 8 / 12 / 16 / 24 / 32; не вводити довільні 17/19/23 |
| Зовнішній safe-area padding | 16 зліва/справа; 12 зверху/знизу понад системний inset |
| Основна кнопка | min-height 52, radius 16; горизонтальний padding 20 |
| Другорядна кнопка | min-height 48, radius 14; без глибокої фаски |
| Іконка | графіка 22–24, touch region min 48×48; padding не може перекривати сусідню дію |
| Контекстна картка | radius 20; padding 16; одна м’яка тінь y=4, spread=0, blur-еквівалент 12, alpha 0.12 |
| Sheet | верхні кути 24; padding 20; нижній padding враховує inset |
| Body / wishes | 16, line-height 1.35, regular |
| Button label | 17, semibold, line-height 1.2 |
| Caption | 13; не для необхідної інструкції |
| Card title | 20, semibold |
| Menu title | 36–44, semibold/bold; до 2 рядків; без товстого контуру |
| Звичайний label | максимум 2 рядки; при 150% змінюється layout, не зменшується текст |

Початковий body font — наявний `Assets/QuietCamp/Resources/Fonts/DejaVuSans SDF.asset`. Перевір реальні гліфи UA/EN/DE. Для заголовка можна використати перевірений жирний варіант цієї сім’ї або вже наявний сумісний шрифт. Не завантажуй випадковий шрифт і не вважай його ліцензію/кирилицю перевіреними. Жодного baked text у UI-спрайтах. Не застосовуй artificial heavy outline як заміну контрасту.

Основні форми зробити нейтральними 9-slice: кремова поверхня, темна CTA, тонкий контур. Існуючий кольоровий Kenney sprite з tint=white не підходить для точної палітри. Не перефарбовуй загальний QcUi так, щоб випадково змінився неохоплений екран; введи явні стилі Primary/Secondary/Quiet/Icon/Attention, потім мігруй споживачів послідовно. Старі прямокутні bevels прибрати, а не накрити новими.

Іконки — одна проста система зі штрихом 1.75–2, округленими кінцями: pause, leaf/shade, ear/quiet, paired tents/friends, doorway/path, rotate clockwise, undo left, redo right, close, settings, journal, check. Іконки Undo/Redo мають відрізнятися напрямом. Не використовуй emoji, різні стилі наборів, літеру замість піктограми або квадрат як неочевидну pause. Нові прості символи створювати як власні vector/code-native ресурси з коректним Unity-імпортом, не AI-картинки з нечіткими краями.

## 4. Layout, камера та сцена

Портретний дизайн. Базові перевірки: 360×640, 390×844, 412×915 та 768×1024; це логічні viewport розміри тестового макета. Окремо реальний Android та notch симулятор. На планшеті контекстна картка максимум 420 логічних одиниць, dialog/sheet максимум 560; gameplay поле масштабується незалежно.

Світ рендериться до країв екрана. Safe area застосовується до UI, не до фону камери. Не створюй верхню/нижню смугу camera clear color через обрізаний camera.rect. Playable board має поміститися в виміряний Rect доступної взаємодії; високі декоративні дерева можуть заходити за його межі, намети/door cells — ні.

У спокійному gameplay стані top reserve 64, bottom reserve 80; viewport не повинен реально вирізати рендер, це геометрія для fit. При відкритій контекстній картці bottom reserve = її виміряна висота + 12. Висота картки: 160–220 при 100% тексті; при 150% до 40% safe-area висоти із scroll. Якщо решта поля стає непридатною для вибору клітин, розгорнута довідка стає sheet, повернення до placement закриває її.

Камера: зберегти ортографію й поточний кут як стартову основу, налаштувати віддалення/target за bounds. Обчислювати проєкції кутів playable board і висоти наметів; перевіряти WorldToScreenPoint проти робочого Rect з відступом 12. Окремий scenery margin не має зменшувати actionable клітини до декоративних крапок. Не просто присвоїти camera.rect=workingRect. Камера лишається full-screen, ціль/orthographicSize налаштовуються під робочу область.

Fit перераховувати при зміні орієнтації/розміру/safe area/layout режиму, після layout pass. При початку pointer capture заморозити камеру до завершення жесту. Не рухати target від кожного toast, rule issue, pointer move або показу тултіпа. Після gesture виконати один відкладений fit. За reduced motion — миттєво. Не додавати в першій ітерації вільний orbit, pinch zoom або pan: вони конкурують із розстановкою й не потрібні для цього UI.

Головна відмінність від рендерів — не лише UI. Потрібні теплий directional light, м’який ambient, глибина далекого лісу, природна трава замість плоскої сіро-зеленої плити. Вечір зберігає контури червоних наметів і вхід; багаття не є єдиним джерелом видимості. Не компенсувати погане світло bloom на всьому екрані, DOF на полі або пересвіченням UI. Ніякої зміни shadeMask через освітлення.

Ціль покриття на 390×844 у спокійному gameplay: не більш як 18% safe-area площі зайнято видимими UI поверхнями, без урахування світу; у Selected до 35%, у modal обмеження не діє. Це цілі композиції для screenshot review, а не заявлена вже виміряна характеристика. Дві постійні великі панелі над і під полем — провал приймання.

## 5. Головне меню — точний стан

Full-bleed табір. У верхніх 12–24% safe area — «Тихий кемпінг» у 2 рядки, Ink на світлому небі. Жодних промослоганів на постійній основі. Settings — у верхньому правому куті, 48×48 touch, кремовий тихий circle/rounded square, не поверх назви. Середні 30–72% — діорама, жодної панелі на наметах.

Нижня область: одна Primary шириною min(82% safe width, 360), висотою 52; під нею дві Quiet-дії в одному рядку: «Стежка рівнів» і «Мої кемпінги», кожна target 48 по висоті. На вузькому екрані/великих шрифтах цей рядок переноситься в 2 рядки без зменшення тексту. Контрастна нижня підкладка допускається лише локально, без темної плашки на половину екрана.

CTA: немає прогресу/сейва → «Почати»; є незавершена сесія → «Продовжити»; сесія завершена, є доступний наступний рівень → «Продовжити»; завершено повну доступну кампанію → «Мої кемпінги», дубль Quiet Album прибрати. На межі демо маршрут визначає canonical progression/entitlement, не перевірка CompletedCount==60. Не дублювати правила доступу в UI. Відсутність ContinueTarget не означає автоматично «почати перший».

Статична ілюстрація меню не повинна «дихати» через постійне масштабування. Жива діорама: лише делікатний вогонь/невеликий рух верхівок, без циклічного руху камери. Жодних обов’язкових тапів для появи кнопок. Після повернення з гри CTA й прогрес оновлюються; double tap запускає один перехід.

## 6. Прогресивне розкриття інформації

Розділи інформацію на 4 рівні: I — короткий affordance («Гості · 2»); II — ім’я та всі умови вибраного гостя; III — точне пояснення одного правила з накладкою на поле; IV — повна довідка в sheet. Рівень II відкривається одним явним натисканням, III — натисканням рядка умови, IV — через «Докладніше»/довідку. Гравцю не треба знати gesture, щоб дістатися жодного рівня.

Перший вхід у новий рівень: показати компактний sheet «Сьогодні на галявині» з переліком усіх гостей і всіх обов’язкових умов. Ця початкова розгортка — свідомий короткий виняток із мінімалістичного idle. За потреби scroll; інформація доступна до першого ходу. Кнопка «До галявини» явно закриває sheet. Ніякого timer auto-dismiss. Після повернення до незавершеного рівня повторно не нав’язувати sheet: лишити вкладку «Гості».

Побажання надалі зберігаються в «Гостях» і в контекстній картці намету. Завжди показувати всі обов’язкові умови вибраного гостя — shade і quiet можуть існувати разом, friends може бути кілька. Не підмінювати перелік одним WishKey. Спільний прохід пояснити в брифінгу; у картці можна мати короткий рядок «Прохід до входу».

Не ставити '?' на кожен елемент. Одне місце допомоги: pause sheet → «Як грати» / «Підказка». Після невдалої Check з’являється contextual «Показати» та «Підказка». Довге натискання може дублювати tooltip, але не бути єдиним способом прочитати правило.

## 7. Gameplay — видимість за станами

Імена нижче описують стани, не вимагають по класу на кожний. SelectedGuestId, placements і completion не дублюються в новому «UI game state». UI зберігає тільки контекст презентації: відкрита панель, вибране пояснення, transient hint revision.

| Стан | Видимо | Приховано / поле | Вихід |
|---|---|---|---|
| Briefing | Sheet гостей, «До галявини» | Placement заблокований; світ видно під scrim | Явне закриття → Idle |
| IdleUnplaced | Зліва маленький chapter pill, справа pause; унизу «Гості · N»; undo лише якщо є історія | Немає rule chips, текстових карток, action toolbar, Check Primary | Tap guests → Tray; tap tent → Inspect |
| Tray | Компактна нижня стрічка гостей з іменами, схемними піктограмами всіх wishes, статусом; selected wish card | Частина поля лишається вільною; sheet не ховає доступ до умов | Tap нерозміщеного гостя → ArmedNew; close → Idle |
| InspectPlaced | Коротка картка гостя; усі wishes; «Перемістити», rotate, More | Немає ghost; світ не змінюється; решта гостей не підписана | Move → ArmedMove; tap another tent → Inspect іншого; background/Back → Idle |
| ArmedNew / ArmedMove | Картка, rotate, «Скасувати»; двері й footprint вибраного | Check/Undo/Redo приховані; прив’язка до нового guest чи старої позиції явна | Tap вільної anchor cell → attempt; drag → Dragging; cancel → попередній стан |
| Dragging | Ghost+footprint+door, одна relevant rule overlay; маленький status capsule за потреби | Панель деталей стиснута, pointer capture, камера нерухома; жодного modal auto-open | Valid release → commit; hard invalid release → відкат; cancel → відкат |
| IdleAllPlaced | Chapter/pause, маленький «Гості»; Primary «Перевірити» і доступний undo | Без постійних wishes; gentle CTA без pulse | Check → Checking; tap tent → Inspect |
| Checking | Одноразове блокування Check до синхронного/асинхронного результату | Не змінює розкладку; не запускає два completion | Report → Issue або Completion |
| Issue | Одна картка конкретної проблеми; «Показати», «Гості», «Підказка» | Не фарбувати все поле; жодної серії toast | Fix/recheck/dismiss; stale issue не лишається після revision |
| HintExplain/Area/Move | По одній підказці/накладці, кнопка наступного кроку | Немає координат (x,z) для користувача; не автокомітити рішення | Явна наступна підказка/закриття/зміна revision |
| Paused / Settings / Journal | Один modal поверх; tabs наявних розділів | World ввід і нижні hotkeys блоковані | Back закриває лише верхній modal |
| CompletionReveal | Світ без rule overlays, grid підтихає, 0.6–0.8 с споглядання | Немає повноекранного dialog; mutation відключена | Далі немодальна completion card; можна показати її відразу tap/Back |
| CompletionActions | «Табір готовий», Primary «Наступна галявина», Quiet «Залишитися»/«Мої кемпінги» | Без оцінок, зірок, конфеті й urgency | Next canonical route; Stay → postcard |
| Postcard | Галявина, один чіткий pill «Продовжити» min 48 target | HUD схований; стан завершеного рівня read-only | Tap pill/Back → CompletionActions |

Повний мінімалізм: важливі exit/continue affordances не зникають від бездіяльності. Idle без таймера приховування control targets. Дозволено сховати підказку «Торкнися намету» після відповідної реальної дії. Ніяких невидимих зон «торкнися будь-де, щоб повернути інтерфейс» як єдиного шляху.

## 8. Картка гостя, керування наметом і жести

Відкрита картка прикріплюється до нижнього UI slot, а не плаває за наметом під пальцем. Верх: ім’я 20 + мала кнопка close 48. Далі 1–4 короткі рядки умов, кожен з іконкою/статусом і target мінімум 48 при інтерактивності. Якщо рядків багато, картка стає scroll, не обрізає останню умову. Нейтральний стан до перевірки — без зелених галочок, які помилково обіцяють успіх.

Нижні дії Inspect: «Перемістити» Primary/Secondary за контекстом, Rotate 48, More 48. Remove та Redo в More sheet із текстовими назвами; Undo також доступний у pause «Історія ходу». Remove має label «Повернути намет» і підпис «Намет повернеться до гостей. Дію можна скасувати», якщо canonical history це дозволяє. Оскільки дія оборотна, зайве підтвердження не потрібне. Повторний вибір того самого намету не закриває картку несподівано.

Контракт tap/drag:

1. PointerDown запам’ятовує target, позицію, pointerId; ще не мутує, не викликає TryCommit, не забирає намет зі State.
2. PointerUp без перевищення 8 логічних одиниць руху = tap. Поставлений намет → Inspect. Порожнє поле в Idle/Inspect → зняти selection, без move. Порожня anchor cell в ArmedNew/Move → одна PlacementCommand.
3. Рух понад 8 одиниць із pointer capture на поставленому наметі → Dragging. До threshold не піднімати намет. Після threshold є ghost, committed pose лишається джерелом істини до release. Tap-control «Перемістити» забезпечує повний маршрут без drag.
4. Обрана нерозміщена картка → ArmedNew; наступний tap на полі ставить її за canonical anchor convention. Drag із картки не робити обов’язковим і не конфліктувати з горизонтальним scroll tray; у першій ітерації drag from tray вимкнений.
5. Ghost для touch показувати вище пальця приблизно на 48 логічних одиниць як стартовий параметр; координату candidate рахувати з того самого visual pointer offset та grab offset. Не показувати ghost над однією клітиною, комітячи іншу. Для mouse offset=0. Під час gesture offset сталий, біля країв краще обмежити допустиму target area, ніж перескакувати між offsets.
6. Другий touch не перехоплює drag і не повертає намет одночасно. Жести над UI не йдуть у world. Tap, який закрив panel, повністю споживається і не ставить намет у цьому ж кадрі.
7. PointerUp над UI/поза полем/поза екраном при drag → cancel. TouchCanceled, focus loss, scene transition, відкриття modal під час capture → cancel + release capture + rollback до committed pose. Нуль нових commands/saves за cancel.
8. Hard-invalid release → ghost повертається/зникає за 160 мс; залишається Armed з поясненням, щоб повторити спробу. Поставлений намет повертається точно в committed pose. Unplaced лишається unplaced. Повідомлення не відкриває modal. Немає нескінченного ghost у невалідному місці.
9. Soft rule issue не блокує TryCommit, якщо report.CanCommit=true. У UI нейтрально показати потребу перевірки; не змінювати правила гри заради «зеленої» картинки.
10. Rotate preview → змінюється тільки candidate. Rotate selected committed → одна canonical команда; якщо hard invalid, стан не змінюється й показується пояснення. Undo/Redo не виконувати всередині активного preview: спочатку explicit cancel, потім дія.

Android Back/Escape: Transition → спожити; top modal → закрити top; active drag/preview → cancel; локальне пояснення/hint → закрити; Inspect/Tray → згорнути й очистити selection; idle → pause. Завершений postcard → показати CompletionActions. В меню дочірній екран → Main, settings із pause → назад у pause. Не відкривати pause поверх активного ghost.

## 9. Правила: точна мова та просторове пояснення

`CampSession` і `RuleEvaluator` — джерела істини. Відображення не вводить власний solver/BFS/підрахунок метрів. Дані накладок брати з report/наявних domain operations або розширити read-only result, не дублюючи алгоритм у HUD. Художня тінь дерева не дорівнює logical shade cells.

| Правило | Короткий текст | Докладний текст / overlay |
|---|---|---|
| Shade | «Намет у тіні» | «Усі клітини намету мають бути в зоні тіні». Показати саме level.shade штрихуванням/листяним мотивом, а не screen-space тінню дерева; footprint і відсутні клітини виділити окремо. |
| Quiet | «Подалі від вогнища» | «Від кожної клітини намету до найближчого джерела шуму — понад 2 клітини по рядах і стовпцях». Показати Manhattan зону для всіх noise cells, не коло радіусом 2 метри. Перешкоди не гасять правило. |
| Friends | «Поруч із {friendName}» | «Між входами — не більш як 3 кроки вільними клітинами». Показати BFS-прохід; не пряму лінію через намети, не Manhattan без перешкод. |
| Path | «Вільний прохід» | «До входу має вести шлях від входу на галявину». Вхід намету — doorCell поза footprint. При блокуванні виділити doorCell і першу локальну перешкоду, якщо її визначає canonical результат. |
| Missing | «Ще чекає місця: {name}» | Відкрити tray з відповідним гостем. Не змушувати шукати його у світі, де намету ще немає. |
| Overlap/bounds/blocked | «Тут намет не поміститься» | Footprint + конкретні заборонені клітини з report. Не тремтіння всієї камери. |

До Check глобальні невиконані умови не створюють масову помаранчеву тривогу. Після Check вибрати одну проблему: missing → path → персональні shade/quiet/friends; у межах категорії стабільний порядок гостей з LevelData, потім стабільний code order. Показати «1 з N», якщо проблем кілька; N=кількість actual report issues, не випадкова кількість підсвічених клітин. Додати next/previous у деталях. Hard-invalid placement пояснюється негайно незалежно від цього порядку.

Issue card залишається до dismiss/виправлення/нової перевірки. Таймером не ховати умову, яку гравець читає. При новому BoardState.Revision перевірити актуальність; більше не показувати старий warning біля нового розміщення. Якщо новий report ще не отримано, прибрати стару накладку, а не вгадувати нову помилку.

Одночасно: один educational/issue panel + одна overlay-category. Вибір shade прибирає friends overlay; двері/selected footprint можуть лишитися як контекст. Усі overlay елементи raycastTarget=false / не на selectable collider layer. Не заливай землю непрозорими кольорами. Контур 2–3 логічні px, зональна заливка alpha ~0.12–0.20; high contrast додає патерн і товстішу лінію.

Прохід/footprint/grid з’являються за 100–140 мс при Armed/Dragging, після commit згасають за 180 мс. У Idle сітка — тонкі природні шви між плитками, без синьо-помаранчевої шахівниці. Сітка, яка потрібна для точного ходу, ніколи не зникає під час drag. Зміна grid contrast не змінює геометрію collider чи anchor mapping.

## 10. Підказки, повідомлення та навчання

Hint flow зберігає Explain → Area → Move. Відкриття дає пояснення, далі явні «Показати ділянку» і «Показати приклад». Прогресивний reveal не пропускає відразу до рішення. Area малює доступні клітини від HintService; Move показує тимчасовий ghost без state mutation. Не виводити користувачу `→ (x,z) ↻90°`. Замість цього «Спробуй поставити намет Анни тут» із named guest і visible preview.

Результат hint має revision. Якщо selection/board/level змінено, закрити стару overlay і скасувати/ігнорувати pending completion відповідного запиту. Solver timeout → «Не вдалося знайти підказку. Спробуй ще раз» + Retry/Close; не показувати вигаданий хід. Не вмикати рекламу чи витрати ресурсів задля отримання базового пояснення в цьому redesign. Поточний AdService stub не означає наявність реального reward flow.

Message slot один: save/error priority вище placement issue, вище навчального nudging, вище декоративного success. Ідентичні повідомлення дедуплікувати за code+guest+revision. Не накопичувати чергу із десяти застарілих toast. Невиконане правило — persistent contextual card; повідомлення «Хід скасовано» може зникнути через 1.8–2.2 с, а стан undo/redo все одно видно в діях. Save failure не зникає як успіх і не обіцяє збереження.

TutorialDirector завершує крок лише реальною відповідною дією. Зберегти це правило: текст «Поверни намет» не ховати через 3 секунди, якщо гравець ще читає. Один coachmark одночасно, з прив’язкою до реального target, плюс зрозуміле «Сховати пояснення»; сховане пояснення можна відкрити в «Як грати». Не ставити темний spotlight на всю галявину для кожного кроку. Пояснення нового правила показати до першої дії, де воно впливає на рішення.

Приклади потрібних локалізованих рядків: «Торкнися намету, щоб дізнатися побажання»; «Обери місце на галявині»; «Вхід має лишатися вільним»; «Трохи далі від вогнища»; «Табір готовий»; «Залишитися на галявині». Поетична мова допустима в привітанні/завершенні, точні числа й причини — у правилах. Не замінювати конкретну умову фразою «Знайди гармонію».

## 11. Другорядні екрани та порожні стани

Pause — компактний sheet до 55% safe height: «Повернутися до табору» Primary; «Побажання гостей», «Підказка», «Налаштування», «Стежка рівнів», «Головне меню» у вертикальному списку target 48. Короткий екран → scroll. Це список доступних дій, не шість одночасних Primary-кнопок. Не робити nested sheet більше одного рівня без Back.

Settings — та сама спільна SettingsPanel для меню і Camp. Групи «Звук», «Зручність», «Мова й вигляд». Volume sliders без декоративних гістограм; звуки за наявними buses. Text scale, reduced motion, calm pace, high contrast, haptics зберігаються штатно. Не скидати прогрес при зміні мови/теми. CalmMode продовжує множити ambient/панельні motion durations через MotionScale=1.6; pressed feedback лишається швидким. ReducedMotion має пріоритет.

Стежка рівнів — вертикальний список/легка стежка карток за главами, поточний рівень одразу у viewport. Тільки реальні ids і доступність від ProgressionService. Display labels — «Галявина 01»/локалізована назва, не QC001 чи QC_TEST. Completed має check + текстовий статус; locked має lock + «Спочатку заверши попередню галявину». Без прогрес-лінії, яка обіцяє неіснуючі глави. Чотири десятки вузлів не створюють тяжкі canvas/material instances без потреби.

Альбом — «Мої кемпінги». Текстова картка з назвою та номером уже може бути якісною; згодом thumbnail саме збереженої розкладки. Поточний AlbumSaveData містить placements/levelId, а не готовий thumbnail. Якщо реалізуєш thumbnails: додай окремий presentation cache, відновлюй read-only render із placements, не підміняй запис AI-фоном. Missing thumbnail → нейтральний рисунок намету, назва, кнопка перегляду; thumbnail не є source of truth. Не видаляй запис через відсутність картинки.

Empty album: «Тут з’являться твої затишні галявини» + «До гри». Повторний запуск завершеного рівня не має переписувати збережену листівку до нового explicit completion. Точну політику старого replay/album збережи або явно протестуй при зміні; не перетворюй tap album на незаявлене очищення прогресу.

DemoComplete/платний доступ: спокійний окремий екран з точними наявними можливостями; повернення доступне. Не вигадуй ціну, purchase flow, subscription або «без реклами», якщо це не підтверджено кодом/продуктом.

Loading: показувати індикатор лише якщо завантаження триває понад 350 мс; не вигадувати progress %. За помилки реальний Retry/Back, без вічного spinner. Після втрати focus скасувати gesture, відновити підтверджену розкладку; обмежити catch-up декоративних анімацій.

## 12. Анімація — точні часові контракти

Один motion vocabulary. UI motion через наявний GameServices.Motion / IUiMotionService; DOTween за потреби лише за тим самим owner/cancel/reduced-motion контрактом. Не додавати паралельний global tween manager. UI transitions працюють за unscaled time й завершуються при pause; один конкретний owner кожної sequence.

| Подія | Звичайний режим | Reduced motion |
|---|---|---|
| Button down | 65 мс, scale 1→0.985, затемнення до 0.96; OutQuad | Тільки колір, без scale |
| Button release | 100 мс до scale 1; без overshoot | Колір за 60 мс |
| Context card open | opacity 0→1 + y 10→0 за 180 мс, OutCubic | opacity за 80 мс |
| Context card close | opacity 1→0 + y 0→6 за 120 мс, InQuad | opacity за 60 мс |
| Sheet open | y 24→0 + opacity за 220 мс; scrim 180 мс | opacity 100 мс |
| Sheet close | 160 мс; input block живе до кінця | opacity 80 мс, той самий input contract |
| World selection | outline fade 100 мс; lift до 0.04–0.06 world units лише для armed/drag | outline instant; без lift |
| Valid placement | settle 120 мс; без bounce і scale tent geometry | final pose instant |
| Invalid release | return-to-committed 160 мс; attention outline 180 мс | instant rollback + outline |
| Camera refit | 220–280 мс OutCubic, тільки поза gesture | instant |
| Check success | grid fade 240 мс, світло blend 600 мс, reveal 600–800 мс | final light/grid одразу; card ≤100 мс |
| Completion card | fade + y=8 за 180 мс, після reveal | 80 мс opacity |
| Scene transition | спільний тихий cream/forest fade: cover 180–220, reveal 220–260 мс | до 100 мс на фазу, без iris/curtain руху |

Cover утримується скільки потрібно для load; секунди завантаження не «прискорюються» таймером анімації. Поточні Doors/Iris/Curtain можна замінити налаштуванням наявного transition service, не створюючи другий loader. Не ламати lease/busy контракт. Якщо style неможливо змінити без широкого рефакторингу пакета, мінімально розширити існуючий service й перевірити всі існуючі transitions tests.

Interruptibility: Open→Close→Open з однією target state, kill/reverse попередній tween; не чекай завершення прихованої черги. Об’єкт inactive після закінчення close; невидимий panel не лишає raycast blockers. При destroyed owner callbacks більше не торкаються UI. Event LevelCompleted не породжує дві анімації/дві картки. LayoutGroup не повинен перезаписувати animated anchoredPosition: анімувати wrapper, а layout керує slot.

Ambient: вогонь слабко змінює emissive/intensity, без різкого flicker; дерева рухають тільки верхівки ≤0.5–1° з періодом 6–10 с. Це стартові художні параметри, увімкнути лише після профілювання. ReducedMotion вимикає sway, camera drift, parallax, пульсацію іскр. Не робити idle pulse на Check, flashing hint чи повторювані рекламо-подібні ефекти. На Low можна прибрати декоративні частинки, але всі rule overlays й інформація зберігаються.

## 13. Звук і тактильність

Використовуй наявні ui.click/ui.back, placement.commit/rotate/undo, rule.invalid, sfx.rustle та реальний catalog. Не посилатися на неіснуючі clips. Одна дія → один звук; не грати click одночасно в Button, action handler і scene host. Тихе шурхотіння selection, м’який tap placement; error тихий, без сирени. Ніякого looping sound на pointer move.

Ambience не переривається при відкритті картки. Modal може знизити gameplay SFX/ambience приблизно на 3–6 dB зі smooth transition 200 мс; не записувати ducking у користувацьку гучність і не перемножувати duck при кожному вкладеному modal. Якщо audio API не підтримує transient duck, спершу зберегти стабільний звук, а не мутувати settings.

Haptic один короткий на successful commit, якщо settings.haptics і платформа підтримують. Без вібрації на кожну клітину drag. Error може мати один м’який відгук, але причина завжди видима. Тиша й вимкнена haptic не приховують успіх/помилку.

## 14. Доступність, читабельність та відсутність пасток

Проєктний мінімум targets — 48×48 логічних одиниць і перевірений розмір на пристрої; не посилатися лише на QcUi.MinTouch=88. Targets не перекриваються, між сусідніми бажано 8. Контекстне правило читається на 100/130/150%; звичайні рядки не обрізаються ellipsis. Кнопки можна перенести в 2 рядки, sheet прокручувати. Значки мають явні підписи в detail/menu, а доступні метадані — через реальний accessibility bridge, якщо він є; не вважай TMP.text автоматично підтримкою screen reader.

Мета контрасту body text ≥4.5:1, значущих ліній/іконок ≥3:1 на фактично скомпонованому фоні. Це орієнтири якості за WCAG, не заява про сертифікацію Unity-гри. Перевірити остаточні screenshot кольори, а не лише hex пари. Світлий текст на вогнищі потребує локальної темної підкладки. High contrast прибирає прозорість панелей і підсилює контури/патерни.

Focus видно, order: context actions → primary → navigation; hidden buttons вилучені з navigation. Відкрив modal — focus на заголовку/першій релевантній дії, закрив — повернув до trigger, якщо він існує. Усі core moves доступні tap та клавіатурною маршрутизацією наявних actions; drag не є обов’язковим. Escape повторює hierarchy Back. Стан disabled має пояснення, якщо причина неочевидна.

Не вважай «усі підказки сховані» виконанням мінімалізму. Для новачка видно гостей, напрям входу при дії, потрібне правило й шлях до допомоги. Режим «Показувати пояснення» можна додати як persisted presentation preference, але default уже зрозумілий без нього; нова настройка не є заміною якісного основного UX.

## 15. Технічна реалізація та ownership

1. Збережи GameServices, QuietCampBootstrap, QcActionHandler, ScreenRouter, IGameplayInputPolicy і UiContextStack. Вони вже є композицією цього проєкту. Не переносити сюди Zenject-graph Moyva і не додавати bootstrap дубль у scene.
2. CampSession володіє State/SelectedGuestId/історією/completion. Жодного прямого редагування placements із кнопок/анімації/preview. Один вхід через canonical commands; presentation читає snapshots/events.
3. Ввести один plain C# presenter/coordinator станів HUD, якщо це реально зменшує змішування UI creation/input state. Можлива назва `CampHudPresenter`; не обов’язковий interface без seam. Відокремити обчислення видимості як pure presentation logic для тестів. Не створювати нову feature «UI gameplay rules».
4. MenuScreens/CampHud лишаються точками UI композиції або делегують конкретним views; не залишати старі root panel під новими. Видалення runtime-created UI безпечно після перевірки refs, але serialized types/assets не видаляти лише за C# grep.
5. Візуальні токени й durations — одна проектна JSON-конфігурація, наприклад `Assets/QuietCamp/Resources/QuietCamp/Presentation/ui_theme.json`, відповідно до наявного QuietCamp JSON loading. Load → Validate → Freeze → Consume. Не створювати ScriptableObject-копію або другий theme JSON у Moyva. User settings — наявний persisted SettingsSaveData, не designer theme.
6. Asset references — наявний каталог/узгоджений runtime asset path, без AssetDatabase у runtime. Нові `.meta` створюються Unity та зберігаються. Не перезаписувати AppIcon або попередню графіку без причини.
7. UI hierarchy: CanvasRoot містить presentation background (меню), SafeArea, overlay/transition layers. SafeArea містить MinimalHeader, ContextDock, MessageSlot, ModalLayer. Gameplay world окремо. Exact names можна адаптувати лише разом з усіма Find-path consumers; жодних тихо зламаних string refs.
8. При відкритті modal: скасувати active gesture; acquire input block і push context до початку fade; нижні hotkeys blocked. При closing modal: сам modal уже не приймає нові команди, scrim і block lease живуть до кінця fade; release у завершенні або Dispose/finally. Не release block на старті close, коли видимий modal ще накриває поле.
9. Прихована група: alpha=0, interactable=false, blocksRaycasts=false; потім inactive за потреби. Прозорі decorative Images і TMP labels raycastTarget=false. Touch-up, який закриває modal, не має пройти в placement після release lease.
10. Підписки Evented/Changed, input capture, temporary materials, hint requests, tween owners і contexts мають Dispose/OnDestroy cleanup. Вихід зі сцени посеред анімації/підказки не лишає static subscriber або invisible blocker.
11. Спочатку дані/стан, потім візуальна анімація. Не save із tween callback. Save failure показується через canonical notification path. Перемога/альбом не втрачаються, якщо користувач закрив застосунок під час reveal.
12. Settings updates застосовуються без перезапуску сцени: мова перебудовує preferred sizes; textScale оновлює також PlainText, не тільки LocalizedLabel; reducedMotion завершує поточну motion до правильного final state; highContrast оновлює матеріали поверхонь.

## 16. Робота з ресурсами і продуктивністю

Готові AI-фони підходять для меню/художнього референса, не для interactive board. Новий пакет складних PNG для кожного UI state не потрібен. Потрібні: нейтральна 9-slice surface/CTA, одна узгоджена система невеликих іконок, прості overlays і TMP. Стани — tint/outline/layout, не десятки texture variants.

Не створювати Material на кожний кадр pointer move. Поточний ShowPath/HidePath з destroy/rebuild перевірити profiler; перестворювати overlay тільки при зміні anchor/rotation/типу пояснення/revision. За потреби pool клітин за розміром board. Не додавати «universal pooling framework» заради 49 клітин. Use shared material/MPB там, де це коректно з URP batching; перевірити на реальному renderer.

Read-only UI не викликає RuleEvaluator.Evaluate кожен frame. Показ змінюється за event/revision; прогрес solver pump працює в обмеженому бюджеті наявного HintService. Декор не має selectable colliders і не перекриває picking намету.

Орієнтир — стабільні 30 FPS на обраному Android середнього класу; 60 як опція. Це ціль перевірки, не гарантія. Зробити baseline і after на тому самому пристрої/build/quality. Не вводити нові postprocess renderer features до доведеної потреби. Відкрита/закрита панель не повинна запускати perpetual Canvas rebuild. Постійний ambient motion не має перемальовувати весь UI Canvas.

## 17. Порядок виконання з конкретними воротами якості

Етап A — поточний baseline: clean build/status, реальні screenshots MainMenu/Camp/Settings/Completion, маленький список наявних blockers. Зберегти тимчасові логи в ignored Temp; не змінювати user work. Не брати старі Screenshots як after.

Етап B — state/input контракт: tap inspection, armed move, drag threshold/cancel, one command per gesture, canonical Check, modal blocking, revision-safe hint. Після цього meaningful EditMode/PlayMode тести. Якщо interaction не працює, не маскувати проблему красивими панелями.

Етап C — tokens/components + MainMenu: одна CTA, secondary links, proper settings, фон/камера. Показати реальний screenshot з build; завершити layouts 360/390/412/768 і 150% text.

Етап D — gameplay: MinimalHeader, guest brief/tray/card, rule details/overlays, errors/hints, completion. Одразу перевірити всі visibility states; жодних старих toolbar/root дублів.

Етап E — світ/анімація/звук: tuning композиції, full-screen camera, gentle lighting, motion owners, reduced motion, audio dedup. Відмовитися від ефекту, який шкодить frame time або читабельності.

Етап F — вторинні екрани, localization/accessibility, regressions, Android smoke. Створити пакет after screenshots/video й коротко зіставити з вимогами. Не подавати HTML-схему або рендер як доказ реалізації Unity.

## 18. Обов’язкові сценарії перевірки

| № | Дія | Очікуваний результат |
|---|---|---|
| T01 | Fresh save → Main → Start | Одна CTA, перший доступний рівень, briefing до першого ходу |
| T02 | Resume із сейва | Відновлено точні placements; briefing не нав’язується повторно |
| T03 | Tap placed tent без руху | Inspect, жодного BoardCommitted/запису історії/сейва |
| T04 | Armed move → tap empty cell | Одна команда; одна audio подія; правильний anchor і door |
| T05 | Drag > threshold → valid release | Candidate відповідає ghost; один commit; camera не рухається під пальцем |
| T06 | Drag → overlap/bounds/UI/offscreen | Відкат; State/Revision/history не змінені; ясне пояснення |
| T07 | TouchCanceled/focus loss/scene exit | Нуль commit, capture released, немає ghost/blocker leak |
| T08 | Second finger під час drag | Не перехоплює pointer, не запускає rotate/menu |
| T09 | Soft quiet/shade/friends issue | Допустимий commit з report.CanCommit; completion тільки explicit Check |
| T10 | У гостя shade+quiet+friends | Усі умови доступні в briefing і картці; жодної втрати через WishKey |
| T11 | Check із кількома issues | Одна стабільна проблема, correct N, навігація проблемами; overlay exact cells |
| T12 | Fix/Undo/Redo після issue | Revision актуальна, stale overlay відсутня; правильний undo/redo availability |
| T13 | Hint ready після нового ходу | Старий result відкинуто; жодного autoplace; timeout має Retry |
| T14 | Open/Close/Open panel швидко | Один final visible state, без double subscription/lease; no click-through |
| T15 | Back із detail/armed/modal/idle | Правильна послідовність розділу 8, не exit із втратою розкладки |
| T16 | Check success + одразу suspend | Прогрес і альбом збережено незалежно від анімації |
| T17 | Completion → Stay → Back | Чистий read-only світ, видимий шлях повернення до дій |
| T18 | UA/EN/DE + 100/130/150% | Усі гліфи/умови/дії доступні; ні горизонтального overflow, ні tiny auto-size |
| T19 | ReducedMotion toggled під час tween | Правильний final state без руху, нуль stuck input blocks |
| T20 | HighContrast, sound/haptic off | Усі правила й результати зрозумілі без кольору/звуку/вібрації |
| T21 | Boot→Main→Camp→Main + direct Main/Camp | Один host/Canvas/EventSystem, Console без нових errors |
| T22 | Малий екран/великий inset/tablet | Full-bleed world, controls у safe area; поле й door cells доступні |
| T23 | Повна кампанія/межа демо/порожній альбом | CTA веде в правильний існуючий маршрут, немає фальшивого purchase або reset |
| T24 | 15 хвилин build + багато відкриттів panel | Немає накопичення матеріалів, підписок і blocker; frame time порівняно з baseline |

Використати існуючі `Assets/QuietCamp/Tests/Editor/RulesTests.cs` (у тому числі HardRejected/soft failures/shipped witnesses) та PlayMode `BootstrapPlayModeTests`, `SceneTransitionPlayModeTests`, `UiShaderPlayModeTests`, `AudioPlayModeTests`, `DecorPlayModeTests`. Не переписувати domain tests під новий вигляд. Додати focused tests для state visibility, command counts, cancel rollback і modal lease lifetime, де поточного покриття немає. Тести мають ловити поведінкові регресії, не дублювати literal кольори.

## 19. Приймання та звіт виконавця

Потрібні real Unity screenshots: Menu first/resume; gameplay IdleUnplaced, ArmedNew, Inspect, Drag valid/invalid, Issue shade/quiet/friends/path, HintArea/Move, Pause, Settings, Completion, Postcard. Коротке відео одного циклу: guest → placement → issue → fix → Check → вечірній postcard. Фіксувати device size, textScale, quality, reducedMotion. Не потрібно зберігати інвентар всього проєкту.

Кожен кадр перевір: світ займає основну площу; один візуальний фокус; немає старих плашок; усі літери читаються; тінь UI не забруднює сцену; tent entrance/footprint не сховані; CTA не конкурує з багаттям; видима причина помилки; safe-area правильно працює. Якщо джерело GDD/render відрізняється від кодових правил — кодові правила лишаються правильними, мистецтво адаптується.

У фінальному звіті: що саме реалізовано; які файли канонічні; compile/test результати; посилання на after screenshots/video; виміряні обмеження; що ще не зроблено. Не називати результат «готовим» без перевірки хоча б на реальному PlayMode. Якщо Unity/Android недоступні, виконати доступні перевірки, прямо вказати неперевірене і не підміняти його HTML-результатом.

Виконавець має залишити проєкт у працездатному стані. Не змінювати unrelated packages, gameplay balance, покупки чи build signing. Не вигадувати готові ассети/методи; якщо ресурс відсутній, створити мінімальний потрібний або явно зазначити блокер. Не просити погодження на кожне оборотне UI-рішення, вже зафіксоване тут.

## 20. Що зроблено автором цієї специфікації

Проаналізовано production UI, scene hosts, input/placement, canonical session/rules, motion/navigation, settings/localization, hint contract і релевантні наявні тести. Переглянуто збережені кадри gameplay та раніше створені рендери. Код Unity під час цього аналізу не змінювався, compile/tests не запускалися: це handoff-завдання, а не реалізація.

`interaction-preview.html` поруч — інтерактивна схема основних UI станів поверх художнього фону, не runnable гра і не pixel-perfect заміна перевірки в Unity. Цей документ має пріоритет над спрощеннями схеми, особливо щодо точних правил, gestures, safe-area й збережень.

Схему перевірено в headless Chrome: 12 станів × 4 viewport × 3 масштаби тексту × 2 варіанти inset = 288 комбінацій; не виявлено горизонтального overflow перевірених панелей/кнопок або активних targets менше 48 CSS px. Окремо перевірено відкриття briefing, три умови Анни, пояснення friends, демонстраційний перехід до завершення та повернення з postcard. Це не перевірка Unity accessibility, фізичних dp, domain rules або реального pointer capture. `UI-STATES.jpg` — знімки саме цієї схеми. Перед застосуванням у грі обов’язковий розділ 18.

Технічні першоджерела: [Unity CanvasGroup](https://docs.unity.com/en-us/engine/6000.7/script-reference/unityengine/canvasgroup) пояснює окремі alpha/interactable/blocksRaycasts; конкретну сумісність перевіряти на встановленій Unity 6.x. [WCAG 2.2](https://www.w3.org/TR/WCAG22/) — орієнтир контрасту й керування неістотною анімацією, не нова вимога сертифікації гри. Решта чисел motion/layout у цьому файлі — власні дизайн-рішення для реалізації, не витяги з цих джерел.
