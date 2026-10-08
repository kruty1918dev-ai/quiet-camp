# Інструмент композиції сцен Quiet Camp

## Для чого він

Інструмент редагує **наміри й зв'язки місць**, а не сотні готових координат. Садиба складається з хати, власного паркану, воріт і підходу до дороги; ЛЕП має географічний маршрут і типізовані опори. Невдала обов'язкова композиція блокує bake, а не залишає випадкові уламки ансамблю.

Portable ядро `QuietCamp.Composition` не залежить від Unity чи прогресу гри. Адаптер Quiet Camp додає координати scroll, поверхню й сезонний sampler. Окремого опублікованого UPM-пакета поки немає.

**Стан на паузі 2026-10-08:** CLI та current-source compilation перевірені. Перший native bake, нові screenshots і native streaming benchmark цього pipeline ще не виконані. Main catalog залишається `world-ukrainian-rural-3`, без активного `compositionManifest`. Рендери за 8 жовтня показують попередню roadmap; gameplay-галерея за 5 жовтня показує попередній стан gameplay.

## Де редагувати

Усі шляхи нижче — від кореня репозиторію.

| Файл / каталог | Що містить |
|---|---|
| `QuietCamp/Assets/QuietCamp/Authoring/Roadmap/Composition/manifest.json` | Перелік регіонів, catalogs, coordinate space і compiler revision |
| `Composition/region-0.json` … `region-4.json` | Галявини, landscape zones, routes, ensemble intentions, landmarks, budgets |
| `Composition/templates.json` | Ролі ансамблю, ownership, entrances, локальні розміри й позиції |
| `Composition/assets.json` | Bounds/footprint, pivot, source height, LOD, sockets, wind, season і provenance |
| `tools/scene-composition/examples/` | Три повні приклади: весняна садиба, літнє поле, зимовий коридор |
| `Composition/Staging/` | Окремі anonymous fictional aircraft/ship studies |

Редагувати треба джерела з manifest. Приклади є знімками рецептів, а не активними файлами кампанії. `create_initial_recipes.py` — історичний одноразовий migration script, не поточний спосіб створення/оновлення карти.

Координати X/Z — метри, Z локальний для регіону. Game adapter додає region offset. Роль має локальні координати відносно ансамблю; `parent` задає власника, а не додаткову ієрархічну трансформацію. `height` задає кінцеву висоту моделі. Footprint повинен відповідати геометрії.

Не змінюйте вручну generated transforms, native meshes або `Resources/QuietCamp/roadmap_regions.json`. Stable node IDs, level IDs, order та unlock prerequisites зберігаються.

## Підготовка CLI

Потрібні .NET SDK для `tools/roadmap-pipeline/RoadmapPipeline.csproj` та Python 3 з `jsonschema`. Для contact sheet потрібен Pillow; для переадаптації staging models — NumPy. На цьому host перевірено jsonschema 4.10.3, Pillow 10.2.0 і NumPy 1.26.4; нових установок для цього гайда не виконувалося.

Запускайте з кореня репозиторію, по одній verification job:

```sh
dotnet build tools/roadmap-pipeline/RoadmapPipeline.csproj -m:1 -p:UseSharedCompilation=false
```

Для коротших Bash-команд можна визначити функцію:

```sh
qc_compose() {
  dotnet run --no-build --project tools/roadmap-pipeline/RoadmapPipeline.csproj -- --composition "$@"
}
```

Після зміни C# спочатку перебудуйте CLI. Хеш portable bake містить фактично виконуваний compiler assembly; він відрізняється від native bake hash.

## Робочий цикл на прикладі садиби

### 1. Знайти джерело

```sh
qc_compose inspect r0-homestead-0
qc_compose inspect r0-homestead-0/house
```

Перший запит повертає файл, entity, `sourceHash`, `sourcePointer`, children, candidates і diagnostics. Другий пояснює, який source owner потрібно редагувати для generated child. Catalog asset/template IDs теж підтримуються.

### 2. Зробити адресну правку

Запишіть окремий JSON-файл, наприклад `/tmp/qc-yard-patch.json`:

```json
{"fenceVariant":"picket"}
```

Потім підставте **хеш конкретного source-файла з inspect**, а не source hash із bake:

```sh
qc_compose patch r0-homestead-0 --expected-hash HASH_FROM_INSPECT --patch-file /tmp/qc-yard-patch.json
```

Об'єкти merge recursively, arrays замінюються цілком. Зміна identity/schema, невідомі поля, застарілий хеш або invalid composition відхиляються до запису. Source hash повторно перевіряється після validation. Якщо інший агент змінив файл, зробіть inspect ще раз і врахуйте його правку.

Не рухайте окрему дошку, щоб приховати дефект усього двору. Коригуйте `placement`, масштаб ансамблю чи локальні ролі шаблону. Ворота повинні залишатися на межі, а підхід — з'єднуватися з іменованою дорогою.

### 3. Перевірити композицію

```sh
qc_compose validate
qc_compose compose --dry-run
```

`validate` перевіряє schema, references, ownership, mandatory dependencies, footprints, supports, approaches і бюджети. Композиція всіх регіонів враховує сусідні ділянки й галявини. `nextRoute` потребує спільного endpoint після застосування region offset.

Dry-run не записує джерела чи native resources. Він показує placements, відхилені candidates та semantic diff відносно останнього portable bake. Якщо попереднього bake немає, усі нові instances будуть у `added`.

[Довідник діагностик](DIAGNOSTICS.md) пояснює причини відмови. `ensemble-does-not-fit` означає, що цілого допустимого ансамблю не знайдено; `role-overlap` містить related child IDs; `unsupported-approach` не дозволяє двору отримати підхід через воду.

### 4. Підготувати portable output

```sh
qc_compose bake
```

Це створює `Composition/bake-preview.json` із placements і diagnostics. **Команда не створює Unity meshes і не активує нову roadmap.** Невдалий bake зберігає попередній коректний результат.

## Native bake та реальний preview

Перед запуском Unity прочитайте [AGENTS.md](../../AGENTS.md): host memory/disk, один Editor, worker counts, capped scope/fallback та безпечне узгодження з ADB. Цей гайд не є дозволом на свіжий Editor або player build.

В авторизованому Editor, поза Play Mode:

1. **Quiet Camp → Composition → Bake Main** — перевіряє весь світ, готує immutable indexed chunk assets і лише потім атомарно змінює catalog pointer.
2. **Quiet Camp → Composition → Preview** — виберіть Main, chunk і entity ID; натисніть **Load / Frame**.
3. Увімкніть Top, IDs, Footprints, Connections, Parcels / zones, Chunks / reveal owner або Entrances.
4. **Capture** зберігає PNG та JSON sidecar у `TestResults/roadmap-composition-2026-10-08/`.

CLI-команда нижче тільки показує спосіб відкрити preview; вона не запускає Unity:

```sh
qc_compose preview r0-homestead-0
```

Sidecar містить source hash, entity IDs, projected bounds, chunk ownership і метрики показаних meshes. Bounds допомагають зв'язати дефект на знімку з конкретним patchable owner. Static preview не замінює Game View, navigation, progression reveal чи touch tests.

Для aircraft/ship: **Bake Staging Landmarks**, потім відповідна Gallery в Preview. Staging не змінює unlocks чи історичні puzzles. Ліцензії, оригінали й adaptation hashes зберігаються в `Authoring/Roadmap/Models/StagingLandmarks/ATTRIBUTION.md`.

## Порівняння

Збережіть matching `.png` і `.json` до/після під різними іменами:

```sh
qc_compose compare /path/before.json /path/after.json
```

Результат — contact sheet і diff entity IDs/метрик. Числа не оцінюють художню красу; image review має перевірити зв'язність двору, щільність природи, seams, lighting, seasons і читабельність галявин.

## Перевірки та обмеження

Без Unity:

```sh
python3 tools/scene-composition/test_cli.py
python3 tools/verify_monetization.py --include-editor
```

CLI tests працюють з тимчасовими копіями authoring. Другий сценарій компілює поточні runtime/Editor/test sources проти cached Unity references; він не запускає scene tests, shader compilation або player build.

Native `CompositionRoadmapPlayModeTests.BakeAndValidateMainGallery` треба виконати до `Native360ScrollResizeReentryAndLowMemory`. Dataset на 360 logical nodes / 60 regions повторно використовує реальні meshes основних 30 рівнів; це перевірка масштабування streaming, а не 360 різних сцен. Навігація, reveal, branches і реальний device performance залишаються окремими acceptance gates.

Runtime target: максимум три nearby chunks, одна камера/RT, shared materials, до 32 world renderers і консервативна presentation estimate до 64 MiB. Один native chunk має cap 15 MiB. CPU activation timer не включає native GPU upload; unsupported counters не зараховуються як нулі.

Зараз zones прямокутні, water-crossing template містить anchor, staging вода є placeholder, а abandoned state має restrained tint замість повного damage kit. Immutable старі native revisions не видаляються автоматично: перед окремо дозволеним player build їхні непотрібні ресурси треба карантинувати за межі Resources, зберігаючи rollback.

Усі наявні dirty changes та збереження гравця зберігайте. Після поточної паузи продовжуйте реалізацію лише за наступним запитом користувача.

## Продовження 2026-10-08

Нові source capabilities: closed boundary intervals, independent entrance roles, standing/fallen/broken conductors, distribution service sockets, convex terrain patches і owner-relative gardens/bus bays. Джерела main та три staging recipes перевірено portable; runtime досі legacy. Додано `qc_compose staging-validate` і Dam Gallery. [Точний поточний статус](../../Design/Roadmap/NATIVE-FINISH-2026-10-08-UA.md). Нових native captures немає.
