# Авторинг безперервної роадмапи Quiet Camp

Чинний пілот — одна українська долина QC001–QC005. Projected UI renderer,
`UI/Prepared` і їхні native тести вилучено. Повні дані кампанії, пазли,
історичні рецепти й gameplay-моделі зберігаються.
[Галерея та статус перевірки](../../Design/Roadmap/CinematicPilot/2026-10-10/README.md).

## Джерела та власність

`QuietCamp/Assets/QuietCamp/Authoring/Roadmap/Composition/CinematicPilot`:

| Файл | Призначення |
| --- | --- |
| `valley.json` | П’ять IDs, дорога, parcels, ансамблі та landmarks. |
| `templates.json` | Власність будівель, дерев, огорож, воріт і підходів. |
| `assets.json` | Геометричні розміри, footprint, wind, LOD та provenance. |
| `bindings.json` | Переглянутий vendor model/LOD, GUID і source SHA-256. |
| `world.json` | Спільні координати, camera anchors, межі частин і обірвані дроти. |

Generated transforms та meshes не редагуються вручну. Цілий двір має одного
власника; дерево може проростати крізь споруду лише з явними `parent` і
`growsThrough`. IDs, порядок рівнів і правила відкриття залишаються стабільними.
Рослинність та рельєф готуються офлайн; рух камери не створює геометрії.

## Inspect → patch → validate → dry-run

Один verification job за раз, з кореня репозиторію:

```sh
dotnet build tools/roadmap-pipeline/RoadmapPipeline.csproj -m:1 -p:UseSharedCompilation=false
export QC_COMPOSITION_SOURCE=QuietCamp/Assets/QuietCamp/Authoring/Roadmap/Composition/CinematicPilot
dotnet tools/roadmap-pipeline/bin/net10.0/RoadmapPipeline.dll --composition inspect reclaimed-yard/house
```

`inspect` повертає source owner/template, JSON pointer та SHA-256 конкретного
файла. Підготуйте merge patch у власному тимчасовому JSON, потім:

```sh
dotnet tools/roadmap-pipeline/bin/net10.0/RoadmapPipeline.dll --composition patch reclaimed-yard --expected-hash HASH_FROM_INSPECT --patch-file /tmp/my-yard-patch.json
dotnet tools/roadmap-pipeline/bin/net10.0/RoadmapPipeline.dll --composition validate
dotnet tools/roadmap-pipeline/bin/net10.0/RoadmapPipeline.dll --composition compose --dry-run
```

Arrays замінюються цілком. Застарілий hash, невідомі поля, частковий ансамбль,
непід’єднаний вхід або геометрична колізія блокують запис. Якщо джерело змінилося,
зробіть inspect повторно. [Діагностики](DIAGNOSTICS.md).

Camera anchors у `world.json` уточнюються за native кадрами; перевіряйте hash
цього файла перед записом. Не переносіть координати з generated resources назад
у джерела. Portable `bake` готує placements, не Unity meshes чи player.

## Native bake та візуальна перевірка

За погодженими [AGENTS.md](../../AGENTS.md) правилами: пам’ять ≥1,5 ГБ перед
запуском, один Editor, workers 1/4, приватні PID/proc/network і приховані USB.

```sh
bash tools/render_diorama_editor_isolated.sh --probe
bash tools/render_diorama_editor_isolated.sh --cinematic
```

Цей етап запускає `CinematicRoadmapBaker` і production URP capture: п’ять місць,
чотири переходи, стани прогресу й Low/Balanced landscape. У вже погодженому
Editor доступне **Quiet Camp → Cinematic Roadmap → Bake Five Places**.
Індекс публікується після перевірки імпорту всіх п’яти частин. Старі ревізії,
на які вже немає посилань, прибираються з Resources; rollback зберігає Git.

```sh
bash tools/render_diorama_editor_isolated.sh --pilot-tests
```

QA копія має окремі ProjectSettings, product/save identity та приватні
`Library/ScriptMapper`, `ScriptAssemblies`, `ArtifactDB`, `SourceAssetDB`; спільний mapper може зберегти старі class mappings. Native script bindings перевіряються й оновлюються Editor preflight через `IPrebuildSetup` **до Play Mode**, коли asset import дозволений.
Game View тест перевіряє запуск п’яти
рівнів, перемоги/повернення, старий прогрес, replay, drag, reveal, notch,
portrait/landscape, reduced motion та повторні входи. Параметр `-noaudio`
запитує вимкнення звуку, але стан аудіопристрою Editor не підтверджений;
фактичне звучання не перевірено. Player builds не запускаються.

## Межі доказів

Semantic validity не вирішує художню якість. Переглядайте кадри: читабельність
історичної деталі й каменя, ближній/середній/дальній план, шви, grounding,
щільність рослинності й переходи. Максимум три детальні частини, спільні
матеріали, спрощений дальній ліс. Геометричні бюджети: Balanced 150 тис./12
матеріалів, Low 80 тис./8; draw calls із тінями — 120/80 відповідно.
Editor counters і відеочастота не означають mobile FPS. Native та Game View
receipts мають однаковий source hash. Історичні кадри зберігають свої дати.
