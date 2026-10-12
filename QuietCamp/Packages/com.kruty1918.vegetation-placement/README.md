# vegetation-placement

Ярусна рослинність за екологічними полями, плямами й безпечними об’ємами.

Статус 0.1.0: Core-контракти перевірено; clean-project native import/tests та
візуальне приймання інтеграції гри ще Pending. Ця версія не є заявою production readiness.

Оригінальний reusable UPM-пакет. Алгоритми Core не знають Quiet Camp, сцен,
Resources, AssetDatabase чи шейдерів. API можна викликати з власного editor,
процедурного генератора або іншої системи. Static geometry v1; skinned meshes
не підтримуються. Core не має прихованих Update, файлових writes або глобального random.

## Встановлення

Приватний репозиторій: https://github.com/kruty1918dev-ai/vegetation-placement
Потрібен Git-доступ до репозиторію. Unity Package Manager приймає Git URL
із pinned commit. Embedded package — повна копія; sibling checkout не потрібен.
Для спеціалізованого пакета встановіть scene-placement у кореневий manifest.
Залежності: `{"com.kruty1918.scene-placement": "0.1.0"}`.
Unity adapter baseline — Unity 6000.6; інші версії ще не перевірені.
Core використовує .NET Standard-compatible C# і перевіряється окремим .NET host.

## API та принцип роботи

`VegetationGenerator.Generate`: `VegetationSpec + PlacementContext + IEnvironmentalFields → VegetationResult`.

Source zone компілюється цілком; outputBounds лише розділяє готовий результат. Cell seeds не залежать від camera/chunk. Species задають scale/spacing/tiers/environment. Volumes для стовбура й крони розділяються автором; motionMargin враховує вітер.

Спеціалізовані генератори використовують Y-up / XZ ground-plane. Одиниця —
довільна узгоджена одиниця світу; адаптер гри задає її відповідність метрам.
scene-placement може використовувати власний Up та ISurfaceQuery. TriangleSurface,
HeightFieldSurface і TerrainSurface семплюють Y-up; це не довільний ray tracer.
Unity Terrain повинен бути без повороту/масштабування. Об’єм — фізична форма,
а не лише pivot. Зміна seed не змінює IDs ролей ансамблю.

Дані — звичайні C# об’єкти. Є CancellationToken, stable IDs і explicit seed.
Всі координати й одиниці вхідних даних повинні належати одному authoring space.
Матеріали, модельні IDs, правила й shader binding надає ваш адаптер.
Core result/diagnostics повертаються до створення GameObjects/asset writes.
Original meshes зберігаються; Unity outputs мають явне володіння й release.

## Три частини

- Runtime/Core: portable algorithms and data; assembly `Kruty1918.VegetationPlacement.Core`.
- Runtime/Unity: Unity adapters; assembly `Kruty1918.VegetationPlacement.Unity`.
- Editor: batch bake; assembly `Kruty1918.VegetationPlacement.Editor`. Окремих вікон немає.

## Автоматичні команди

Editor method: `Kruty1918.VegetationPlacement.EditorTools.Batch.Run`
(див. Recipe у Editor/Batch.cs).
Передайте `--authoring-request /absolute/recipe.json` та
`--authoring-output Assets/AuthoringOutput`. Native bake запускається вашим
Unity CLI через -executeMethod. Runtime API не вимагає JSON або CLI.
На хості Quiet Camp Unity запускається тільки через погоджений isolated wrapper.

## Перевірка й приклади

`Tests~/Contracts.csproj` — автономний контракт без Unity/game assets.
Для спеціалізованого пакета: `dotnet run --project Tests~/Contracts.csproj
-p:ScenePlacementRoot=/absolute/path/to/scene-placement`.
У embedded checkout default dependency лежить поруч у Packages.
`Samples~/Example.cs` — мінімальний виклик API на синтетичних даних.
`Tests/Editor/NativeContracts.cs` — native EditMode acceptance у чистому проєкті;
додайте package name у manifest.testables. Ці native tests ще не виконані.
Native Unity/import acceptance звітується окремо; .NET тест не доводить
візуальну якість або mobile FPS.

## Ліцензія

MIT, власний код. Комерційні моделі й їхні фотографії до пакета не входять.

Докладні parameters, принцип роботи й обмеження: [API-UA](Documentation~/API-UA.md).
[Receipt](Validation~/receipt.json) відокремлює Core/cached compilation від native acceptance.


## Rule-based scene composition

`CompositionVegetationStage` consumes the shared layout, moisture and landing exclusions. Species support a bounded yaw range for readable flower faces. Core tests include shoreline habitat and deterministic replay; native acceptance remains separate.
