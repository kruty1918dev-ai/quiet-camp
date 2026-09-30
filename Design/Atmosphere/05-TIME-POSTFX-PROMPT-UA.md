# Промпт 05 — теплий мобільний рендер і чотири фази доби
Застосуй разом із START-HERE-UA.md. Мета — багатий, спокійний вигляд при малому GPU/CPU навантаженні. Тепло виникає передусім із палітри, світла, матеріалів і звуку; постефекти лише завершують сцену.

## Поточні налаштування
Перевірено Assets/QuietCamp/Settings/URP:
- QC_Low: renderScale.85, MSAA1, main shadow map512.
- QC_Balanced: scale1, MSAA2, shadow1024.
- QC_High: scale1, MSAA4, shadow2048.
- В усіх: supportsHDR0, requireDepthTexture0, requireOpaqueTexture0, mainLightShadowsSupported1, shadowDistance20, один cascade.
- QC_GlobalVolumeProfile: Bloom активний, але intensity0; ColorAdjustments активний і нейтральний.
Це serialized assets; фактичне призначення renderer/pipeline, camera post-processing і platform Quality mapping перевірити перед реалізацією. Сам факт існування трьох asset не доводить, що телефон обирає правильний.

## Власник доби
Увесь світ читає один snapshot. Немає окремого Update у світла, звуку й частинок із незалежним визначенням «ночі».
Default: художня фаза рівня, не реальний годинник. Якщо є game-calendar, використати його лише як upstream authority за відповідною ігровою вимогою; не вигадувати дві доби.
Підтримати morning, noon, evening, night; day alias→noon. Unknown/null → noon з одним diagnostic warning. Existing evening лишається evening. Не перезаписувати QC001..QC010 автоматично.

Completion наразі ApplyLighting(true). Замінити це локальним celebration accent: вогник/дуже малий теплий акцент на .5–1с без примусової зміни night→evening. Якщо дизайн вимагає sunset після перемоги, це явна команда єдиному state owner, не прихована зміна з CampSceneHost.

## Вихідні художні параметри
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

## Список ефектів і рішень
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

## Bloom і HDR — явна розвилка
Нині HDR вимкнений. Не встановлювати threshold>1 у LDR і дивуватися відсутності свічення. Базовий шлях: LDR + готовий FireGlow, bloom0, всі якості виглядають завершено.
Опційний High: ввімкнути HDR у погодженому pipeline/camera, підготувати emissive fire materials (значення>1), Neutral tone mapping; Bloom threshold≈1.05–1.2, intensity за фазою .02–.08, scatter.45–.6, HQ filtering Off, dirt texture Off, max iterations орієнтир4. Перевірити існування полів у встановленому17.6.
Вартість HDR render targets, MSAA resolve і bloom заміряти РАЗОМ, а не тільки bloom shader. Якщо HDR+MSAA4 занадто дорогі, High може лишитися LDR; не знижувати чіткість поля заради слабкого halo. Не форсувати HDR на всіх devices.

## Volume та матеріали
Не змінювати sharedProfile asset у Play Mode для доби. Runtime instance один на сцену/owner, dispose після сцени; дозволені components кешовані. Не створювати profile/material щокадру. State interpolator змінює значення тільки під час fade/реальної зміни; steady-state не rebuild LUT без потреби. Grading корекції не мають кодувати фазу вдруге поверх повністю тонованого світу.

UI Screen Space overlay після grading; видимий world-space текст, якщо він є, перевірити на контраст у всіх фазах. Меню та gameplay використовують той самий profile policy. Перехід через menu не скидає mute/quality/reducedMotion.

Старий SkyPalette.Apply викликає DynamicGI.UpdateEnvironment: не використовувати щокадру при phaseBlend. Переважно явний ambient setting; refresh probe/environment лише на безпечній точці за доведеної потреби. Процедурний sky і новий PNG backdrop не повинні малювати два різні горизонти.

## М’яка зміна фази
Для preview/debug:10с color/light blend, audio6–10с, particles плавно змінюють rate, існуючі доживають. Interpolate sun direction без різкого оберту через0/360; для різних night/day directions під cover поставити нову позу. Wind base strength smoothing3–5с.
Коли новий рівень має іншу фазу — встановити її під opaque transition, а не кросфейдити всю галявину під пальцем. На seamless довгому crossfade AI-зображень можливий ghosting крони: це не одна і та сама 3D-сцена. Тому canonical change між рівнями робиться під cover. Не вважати довільне проміжне phaseBlend коректним часом, якщо audio/sky пресети не готові.

## Мобільні бюджети
Числа нижче — цілі приймання, що потребують замірів.
Low:30fps, render scale.8–.85 як fallback, 1×MSAA; нуль post passes; один directional, лише важливі shadows512 або контактні substitutes; no extra light shadows.
Balanced:30fps стабільно, scale.9–1,2×MSAA, shadow1024 короткого радіуса; color grading тільки за ресурсом; bloom off.
High:60fps лише коли p95 вкладається в16.7мс, scale1,2×MSAA спочатку;4× тільки за вимірами. HDR/bloom optional.
Орієнтир додаткової атмосфери відносно чистої сцени: CPU≤.5мс steady state, GPU≤1мс на погодженому середньому пристрої; **це не гарантовані виміри**. На Low спершу зменшити transparent coverage й зайві shadows, а не ламати UI.

Пам’ять: один source1086×1448 RGBA≈6MiB, без mipmaps, до компресії; два≈12MiB. Leaves1254² ще≈6MiB без компресії. Read/Write додає CPU-копії — Off. ASTC має інші фактичні витрати, перевірити Memory Profiler; не рахувати PNG file size як GPU memory. Один quarter-resolution RT має1/16 pixels повного, але world color intermediate/HDR/MSAA можуть коштувати більше самого blur. Не обіцяти «майже безкоштовно».

Quality визначити один раз із підтриманого tier/fallback, дозволити ручний override. Adaptive degradation тільки з hysteresis: наприклад перевищення frame budget10с → один рівень нижче; повертати вище не раніше60с і не під час drag/transition. Розрізняти CPU/GPU bound: scale знижувати тільки коли допомагає. Не запускати тест продуктивності кожен кадр і не перемикати HDR посеред interaction.

Порядок зменшення навантаження: optional blur → bloom/HDR → пилок/near particles → transparent coverage → декоративні shadows → render scale. Зберегти input, text resolution, sound feedback. Ui raycast canvas не rebuild через кожен gust.

## Перевірки
1. Frame Debugger: count passes/RT/copies до й після; Low справді без post/depth/opaque copies, крім тих, які потребують інші наявні системи.
2. Профілювання Development build на телефоні, не лише Editor:10–15хв; p50/p95 CPU/GPU, GC/alloc, thermal, audio DSP і memory.
3. Однакові screenshots чотирьох фаз на3 qualities; ніч читабельна за зниженої яскравості, glow не перекриває rule indicators.
4. Sleep/resume, rapid debug phase switch,30 scene transitions; немає material/profile leaks та безмежного росту streaming voices.
5. Довгий static idle без GC allocations від atmosphere scheduler; events не працюють кожен frame через LINQ/strings.
6. OpenGLES/Vulkan/Metal підтримувані поточним build — shader variants/import компресія перевірені. Unsupported compression має fallback.
7. Звук, вітер і particles насправді реагують на той самий snapshot; зміна ночі не лишає денних птахів, старого світла чи денного задника.

