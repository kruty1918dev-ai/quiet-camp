# Промпт 04 — камера м’яко занурюється в кущі
Відчуття: гравець нахиляється вниз, великі листки наближаються до об’єктива, м’яко закривають краєвид; нова галявина відкривається крізь зелень. Ніякого різкого падіння, teleport flash або шумного кіношного whoosh.

## Місце інтеграції
Канонічний ScreenRouter уже керує переходами, busy/input gate і завантаженням. Розширити його presentation transition, не створювати другого маршрутизатора. Doors/Iris/Curtain для звичайних переходів замінити одним FoliageDive, а не додати поверх. Пауза/налаштування — локальне відкриття панелі без scene plunge. Перший запуск — короткий reveal із cover, без удаваного занурення з порожньої сцени.

Дотримуватися state machine:
Idle → Covering → CoveredLoading → Preparing → Revealing → Idle.
Помилка/timeout/cancel → Recovery; всі lease/input/handles гарантовано відпускаються. Один власник камери в кожний момент.

## Нормальний timeline
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

## Закриття й готовність сцени
Alpha PNG має отвори — він НЕ може гарантувати приховане завантаження. Потрібен окремий повністю opaque color cover на весь framebuffer, включно з notch/letterbox. Не чорнота, а темний колір лісу. Стан Covered дозволений лише коли opacity=1 і покрит весь screen rect. Под cover листя маскує перехід художественно.

LoadSceneAsync і readiness — різні речі. Один Task.Yield після load, як у поточному роутері, недостатній як контракт. Readiness підтверджує: scene root/service bindings готові; level loaded/validated; board render створено; CameraFitter застосований після UI layout; фаза/фон/світло узгоджені; аудіо не дубльоване. Після цього один відрендерений стабільний кадр за cover, тоді reveal. Якщо фон не завантажився, використовувати узгоджений solid-color fallback; не тримати нескінченний lock.

Wait>1.5с: маленьке «Стежка вже поруч…»/спокійний індикатор на cover, без fake percentage. Timeout орієнтир15с для повідомлення й доступного Retry/Назад. Unity scene load не завжди можна скасувати фізично: cancel відхиляє результат/наступні callbacks за generation token, а активну операцію коректно завершує/прибирає. Не показувати напівготову сцену. Після помилки — відновити придатний екран, розблокувати навігацію, зупинити свої sounds. Exception не лишає _busy=true.

## Blur: три рівні
Low: без full-screen blur. Готовий м’який foreground, один opaque cover,2 leaf sprites; зменшення opacity світу допускається лише під листям, не як окремий довгий fade.
Balanced: та сама техніка з4 leaf sprites і тонким edge darkening уже в графіці. Не додавати DOF/MotionBlur.
High (опційно після профілювання): transient downsampled directional blur world color, 1/4 width × 1/4 height, максимум два короткі separable проходи по5 tap. Сумарний видимий радіус≈4–8 logical px, зростає лише260–440мс, зникає до reveal completion. UI/текст/системні кнопки не блюрити.

URP17: RenderGraph pass з явними read/write resources; не читати й писати той самий texture handle. Якщо active color — backbuffer, спершу отримати валідний sampleable intermediate/copy; не семплити backbuffer як texture. Реальний RenderGraph API звірити з установленим17.6, бо приклади6000.0 не гарантують ідентичні сигнатури. Не вимикати RenderGraph заради legacy CommandBuffer.Blit. У звичайному gameplay feature не алокує RT і не змушує всі камери постійно render into intermediate. При camera stack/resize/orientation тимчасові targets і descriptor оновлюються коректно. Не додавати motion vectors чи depth texture для імітації простого руху.

Тест A/B: якщо effect не помітно кращий за вже розмитий near слой — вилучити High blur. Це опція, не умова затишку. [Unity Render Graph](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/render-graph-introduction.html) описує керування проходами й ресурсами.

## Звук переходу
Використовуй наявний sfx.rustle; спершу слуховий A/B із зниженим gain. ambience.gust — для пориву світу, не обов’язковий4с whoosh на кожному переході. На вході rustle може збігтися з природним поривом лише через загальний scheduler, не дві копії.
UI click сухий і короткий. Листя має просторовий рух лише в pan(−.15..+.15), не бігає зліва направо на100%. Doppler0. Echo/reverb на transition rustle вимкнути. Дві точки cue прив’язані до progress, guards від повторного спрацювання; при frame hitch не програвати пропущені cues пачкою. Звукові handles належать transition scope.

Під full cover старий ambience зменшується, новий наростає .8–1.5с; один і той самий wind можна зберегти без restart. Listener стабільний; анімація камери не робить fire раптом ближчим/гучнішим. На mute не грає нічого; ready/pending не залежать від кінця звуку.

## Переривання та доступність
Reduced motion: нерухома camera, без flying leaves, без blur; затишний color dissolve140мс cover + wait +180мс reveal, ті самі gates/readiness. Rustle можна пропустити, залишити один тихий UI cue.
Back/repeated tap під transition не запускає другий load. Одна явна політика: ігнорувати звичайні повторні переходи, системний Back обробити через route intent після безпечної точки, без queue10 сцен.
Application pause: timeline зупиняється, cover не зникає випадково; resume перевіряє поточний load/readiness. Resize: cover негайно підлаштувати, baseline fit перерахувати, повернути новий fit; не відновлювати старі екранні bounds.
Drag cancellation не ставить об’єкт у невідому клітинку. Виключений input не забирає доступ до recovery Retry/Back.

## Приймання
Записати відео з повільною анімацією й scrub кожного frame: немає flash, leaf cutout прямокутника, провалу під землю, накладання UI на завантаження. Direct Camp стартує нормально без попередньої меню-камери. Після30 циклів baseline camera не дрейфує; rotations/sizes/rect рівні актуальному fit; input працює, audio voices/RT не ростуть. Slow load5с і simulated failure перевірені. Телефон не зависає на анімації через shader compilation — потрібні варіанти підготовлені до першого переходу.

