# Реалізація gameplay-атмосфери
Оновлено 30.09.2026. Цей файл описує поточне підключення у Unity; п’ять PROMPT-UA.md лишаються детальним завданням для подальших ефектів.

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

## Сумісність із паралельним переносом
Під час роботи локальні FireFx/Sky/Foliage шейдери перенесли у Kruty1918.Atmos. Цей перенос збережено. Новий код використовує актуальний FireVisual пакета; його API не змінювався. Трава/квіти лишаються на Atmos/FoliageSway з локальними material copies й налаштованими _BaseColor/_SwayAmp. Геометрична анімація дерев, нова URP-модель lighting для пакетного sway та загальний gust scheduler ще описані в промпті03.

## Що лишається завданням із промптів
Переходи камерою крізь листя; новий particle scheduler; справжній спільний порив для дерева/листка/звуку; 3D-positioning та вибіркове outdoor echo; виправлення waveform петлі цвіркунів; повне мінімалістичне UI-перепроєктування. Це не видається за вже реалізований код.

Нова сцена працює в поточному LDR-профілі. Додатковий bloom/HDR/DOF не ввімкнено. Основний об’єм дають світло, матеріали, реальні об’єкти й різні плани. [Промпт05](05-TIME-POSTFX-PROMPT-UA.md) задає опційні профілі після профілювання; [Unity URP](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/integration-with-post-processing.html) описує мобільні обмеження постефектів.

## Перевірка
Звіти Unity зберігаються в ігнорованій папці quiet-camp/Temp/ai/atmosphere/ поза Unity-проєктом: Unity очищає власний Temp при наступних запусках. Перевірено компіляцію, 17 EditMode тестів і 5 PlayMode тестів. PlayMode охоплює фази, наявність шарів, підтримку shader, відсутність рожевих пікселів, недублювання loop, вихід у меню й повернення реальною кнопкою Start/Continue.

Кадри з камери Unity показують world без overlay HUD; це не рекламні рендери й не повні device screenshots. Тести в Editor не є перевіркою fps/нагріву/ASTC на Android чи iPhone. Окрему Android-збірку, яку паралельно виконував інший процес, не зараховано до перевірок цієї зміни.
