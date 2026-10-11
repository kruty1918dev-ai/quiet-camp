# Джерела, запікання та наступне native-приймання

Стан 12.10.2026: усі десять композицій запечені поза Unity в `Baked/`.
Нові рівні ще не опубліковані в gameplay. Поточна вказівка користувача —
продовжувати без Unity; наведені нижче Editor-команди зараз не виконуються.

## Джерело істини

Редагувати [Authoring/places.json](Authoring/places.json), а не binary buffers.
Файл містить IDs, seed, Y-up/XZ координати в одиницях клітинки поля, рельєф,
малі опорні площадки під спорудами, маршрути та їхні входи, чашу джерела,
контур двору/ворота, електромережу, каміння, рослинні зони, палітру ромашок,
профіль деталізації й camera anchors. Bootstrap `refine_first_ten_sources.py`
призначений тільки для переходу старого schema 1 → 2 і не перезаписує schema 2.

`FirstTenEnvironmentBake.cs` — адаптер гри до восьми універсальних Core API.
Він читає чинну gameplay-бібліотеку моделей; її оригінали не змінюються.
Старіння виконується перед розрахунком остаточних опор і bounds. Огорожі
зберігають кріплення; дороги передають коридори рослинності й маршрут мережі.
Рослини мають окремі об'єми стовбура/крони та запас на вітер. Проходи, вода,
споруди, поле й небажані тіні на пазлі обмежують розміщення. Підтвердження
якості цих правил у реальному кадрі залишається окремим прийманням.

## Перевірки без Unity

З кореня репозиторію, послідовно:

```bash
python3 tools/verify_first_ten_environment_sources.py
python3 tools/bake_first_ten_environments.py --validate-layouts
python3 tools/bake_first_ten_environments.py
python3 tools/verify_first_ten_bake.py
python3 tools/verify_first_ten_domain.py
DOTNET_GCHeapHardLimit=0x0c000000 DOTNET_GCConserveMemory=9 DOTNET_gcServer=0 python3 tools/verify_monetization.py --include-editor --receipt Design/Campaign100/2026-10-11/FirstTenRefinement/integration-source-checks.json
python3 tools/build_first_ten_environment_report.py
python3 tools/qa/test_first_ten_environment_report.py
```

Повний bake перевіряє RAM ≥1500 MB. Managed heap обмежено 192 MB;
компіляція та короткі layout-перевірки — 96 MB. Джерела не редагуються під
час роботи. Невдалий bake видаляє свій staging; результат публікується в
immutable каталозі `Baked/<sourceRevision>` тільки після завершення десятки.
`Baked/current.json` вказує актуальну редакцію. Невикористані чернетки без
native-кадрів не є історичними еталонами.

`verify_first_ten_bake.py` незалежно читає всі 50 файлів: checksum, довжини,
нормалі, кінцеві атрибути, індекси, IDs, сюжетні anchors і зв'язки конструкцій.
У receipt записуються hashes джерел; це не доказ освітлення чи художньої якості.
Domain probe перевіряє чинні 100 пазлів, witnesses, незалежний solver,
історичні revisions, збереження й content packs. Compiler check користується
наявними Unity references; він не виконує імпорт або PlayMode-тести.

## Runtime-адаптер

`LevelData.bakedEnvironment` — необов'язкове посилання на immutable manifest.
Порожнє поле не серіалізується: старі рівні та hashes лишаються сумісними.
`BakedCampEnvironment` один раз декодує готові буфери позицій, нормалей,
кольорів і координат вітру. У світі п'ять spatial meshes з матеріалами для
землі, споруд, доріг, дерев і підліску; тіні включені. Розміщення та геометрія
під час гри не генеруються. Власні meshes/materials звільняються при знищенні
оточення; повторні входи треба ще перевірити в Editor.

Для запечених рівнів старі scatter, галявина, канопі та зовнішні декоративні
стежки пропускаються. Пазл, намети, HUD, погода, частинки, звук і reduced
motion залишаються в чинних системах. Авторська камера застосовується після
завантаження оточення. Для історичних рівнів працює попередній шлях.

## Опублікувати й перевірити в Editor пізніше

Для native-прогону спочатку підготувати кандидат оточення командою нижче;
після цього перевірити QC001/QC003/QC006, внести художні виправлення й
повторно запекти. Приймання решти й позначення результату завершеним
відбувається тільки після цього. Команда публікації підготовлена:

```bash
dotnet run --project tools/MonetizationProbe.csproj -- --author-first-ten /home/oleks/Документи/GitHub/quiet-camp
```

Це запуск .NET-інструмента, не player build. Команда перевіряє повну кампанію,
актуальність джерел/буферів, архівує точні чинні revisions, додає посилання
на запечені ресурси тільки першій десятці та зберігає їхні поля/witnesses.
Результат не позначається художньо прийнятим автоматично. Зараз ця команда
не виконувалася: чинний gameplay і captures залишаються вихідними.

Після публікації підготувати `python3 tools/campaign100_capture_sources.py`.
Для окремого «після» каталогу передати `QC_FIRST_TEN_EVIDENCE=1`,
`QC_CAMPAIGN_FIRST=1`, `QC_CAMPAIGN_LAST=10`, `QC_CAMPAIGN_OFFSCREEN=1`
у погоджений `render_diorama_editor_isolated.sh --campaign-tests`. Це native
world-camera кадри без HUD; Game View/HUD перевіряється окремо. Звіт приймає
новий кадр лише за збігом PNG SHA-256, published contentHash і sourceRevision.
Історичний baseline не перезаписується.

Кожен UPM-пакет також має пройти окремий чистий Unity-проєкт:
`prepare_environment_clean_project.py --only <slug> --output <owned-project>`, probe ізоляції та
`run_environment_editor_isolated.sh --project <owned-project> <receipt-prefix>`.
Після цього потрібні синтетичні native-сцени, 20 кадрів, атмосферні відео,
навчання/replay/старі saves, reduced motion і resource-cycle checks.
One Editor, перевірка RAM/диска, погоджена ізоляція й workers 1/4 обов'язкові.
Player builds заборонені. QC_Renderer.asset не змінювати. Editor-виміри не FPS телефона.
