# Roadmap screenshot regression

Нативний набір: `RoadmapStreamingPlayModeTests`. Знімки з Game View, а не зовнішні mockups. Поточний запуск пише у `TestResults/roadmap-production-2026-10-07/`; PlayMode XML і REPORT_UA.md визначають, які сценарії справді пройдені. Старий файл PNG сам по собі не доводить успішність нового тесту.

| Знімок | Сценарій | Перевіряти візуально |
|---|---|---|
| main_menu | Boot → Main, пропущене навчання, RAM save | Видима кнопка налаштувань; контраст і читабельність; cozy palette |
| roadmap_current | Main → Level Path, поточна галявина 26 | Позиція скролу; перехід winter → spring; відсутність порожніх видимих chunks |
| clean_early_spring | Синтетичний маршрут 360, перші вузли | Весняна палітра, модель поля, рослини, освітлені грані, стежка |
| autumn_transition | Синтетичні вузли 15–18 | Узгоджений перехід палітри, дерева, локальний дощ/туман |
| snow_transition | Синтетичні вузли 20–23 | Сніг, голі листяні дерева, ялини, тіні, тепле багаття |
| hidden_future | Frontier 341, дві наступні silhouettes | Немає повних діорам чи cultural/story props за reveal distance |
| visible_branch | Доступна видима заготовка бонусної гілки | Стежка до відгалуження, правильний title/lock, відсутність overlap |
| bonus_preview | Main roadmap → bonus preview | Модальне вікно; input карти заблокований, карта збережена під ним |
| roadmap_uk / en / de | Зміна locale, 130% тексту | Немає обрізаних написів; anchor збережений при CSS remount |
| roadmap_tablet | 1280×800 | Повний viewport, доступні кнопки, правильні bounds і clipping |
| gameplay_return | Main map → QC001 → qc.levels | Повернення в правильний маршрут і відновлення anchor |
| my_camps_return | Map → My Camps зі snapshot QC001 | Одна діорама; lighting і UI узгоджені; replay працює |
| mh17_inspired | Окремий тестовий регіон пам’яті | Вигаданий сад, без debris/evidence reconstruction, gore або popup |
| black_sea | Окремий вигаданий берег | Узгоджена вода/суша, дерева не потрапляють у воду |
| ship_level | **Preview fixture** вигаданого човна | Модель човна, берег і освітлення; це не тест playable ship puzzle |
| kakhovka_inspired | Окремий тестовий регіон заплави | Вигаданий фундамент/рослини; без тверджень про реальні предмети |
| benchmark-story-180 | Самостійний довгий story route | Той самий backend, окремі ID та bounded activation |

Capture helper перевіряє фактичну роздільність, наявність зображення й відсутність масового magenta. Це smoke-перевірка, не оцінка художньої якості. Піксельний golden-diff не використовується для випадкового фону Main та анімованої погоди: перед таким порівнянням необхідно фіксувати seed/time та профіль освітлення. Майбутній automated golden workflow має порівнювати однакові детерміновані сценарії, а не маскувати відмінності великим допуском.

Historical fixtures не експортуються в Resources кампанії. Редакційну перевірку claims описано у PRODUCTION-PIPELINE-UA.md. Нова культурна відсилка потребує першоджерел і відокремлення перевіреного факту від вигаданої географії.
