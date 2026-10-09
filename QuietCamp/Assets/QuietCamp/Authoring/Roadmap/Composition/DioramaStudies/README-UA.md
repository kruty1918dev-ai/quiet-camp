# 12 стильових діорам — editable source

Окремі досліди стилю, погоджений перший обсяг 8–12 місць. Кожна сцена
має власний простір координат; D01–D12 — art IDs, не level IDs. Main не
випікається, кампанія та правила пазлів не змінюються.

`D*.json` описують зони, табірну галявину, дорогу й цілісні ансамблі.
`templates.json` задає двір/сад/пасіку/берег як композицію з ролями.
`bindings.json` прив'язує 17 нових donor моделей до переглянутого каталогу
за GUID, SHA-256 та prefab; комерційні джерела лишаються локальними.
`presentation.json` задає назви, задум, камеру та visual references чинних
рівнів. Вода дослідів — проста проектна геометрія, не несправний Synty shader.

Сценарії пройшли schema та semantic composer: 12 прийнятих ансамблів,
126 структурних instances, 4 підходи, нуль diagnostics. Фотографії та
художній огляд додаються наступним перевіреним етапом.

З кореня репозиторію:

```bash
export QC_COMPOSITION_SOURCE=QuietCamp/Assets/QuietCamp/Authoring/Roadmap/Composition/DioramaStudies
dotnet run --no-build --project tools/roadmap-pipeline/RoadmapPipeline.csproj -- --composition inspect D01.focal
# patch <id> --expected-hash <hash з inspect> --patch-file <json>
dotnet run --no-build --project tools/roadmap-pipeline/RoadmapPipeline.csproj -- --composition validate
dotnet run --no-build --project tools/roadmap-pipeline/RoadmapPipeline.csproj -- --composition compose --dry-run
```

Composer перевіряє досліди незалежно, з повними правилами ансамблів,
детермінізмом і контролем кількості прийнятих ділянок. `bake` для цього
manifest заборонено. Main regression contracts виконуються окремо і
залишаються чинними; це не оголошення проходження mobile budgets.

Не редагуйте `result.instances` у квитанціях зображень: вони отримані
з source. Не запускайте seed script повторно поверх прийнятого дизайну.
