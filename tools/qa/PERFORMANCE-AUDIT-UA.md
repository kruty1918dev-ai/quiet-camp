# Native Editor performance audit

[Карта продуктивності](../../PERFORMANCE_MAP.md) · [Дані](../../docs/performance/2026-10-09/summary.json) · [Capture workflow](DOCS-CAPTURE-UA.md)

Fixture використовує справжній rendered Game View, scene router, world composition та Unity UI. Вона не створює player build і не запускає Editor. `PerformanceAudit` за замовчуванням вимкнено; у player його scopes порожні. Native matrix і foliage regression receipt — у [docs/performance/2026-10-09](../../docs/performance/2026-10-09).

## У вже відкритому Editor

1. Дотримуйся [workspace rules](../../AGENTS.md). Один Unity check за раз. Активний ADB не зупиняй, shared ADB preferences не змінюй. Наявність ADB не дає дозволу на новий Editor launch: використовуй лише наявний Editor потрібного проєкту.
2. Збережи свою scene й створи приватну резервну копію game saves та `ProjectSettings/ProjectSettings.asset`. Зафіксуй hashes початкових dirty assets, game slots і ADB process identity/preferences без публікації особистих saves. Перевір RAM і вільний диск.
3. Скопіюй [PerformanceAuditDriver.cs](PerformanceAuditDriver.cs) у `QuietCamp/Assets/Editor/PerformanceAuditDriver.cs`. Не перезаписуй сторонній файл. Виконай Assets → Refresh і **дочекайся завершення import/compilation до створення request**. Інакше старий driver може прийняти request до перезавантаження assembly.
4. Використовуй окрему product identity з префіксом `QuietCampPerfQA` і новим суфіксом для кожного run. У системному temp directory створи папку `quietcamp-performance-control`. Попередні results спершу архівуй. Request має структуру нижче; `context` — JSON, закодований у string, зі source commit/dirty asset hashes/fixture hash та описом середовища.

```json
{
  "productName": "QuietCampPerfQAUniqueRun",
  "suite": "performance",
  "context": "{\"purpose\":\"authorized native Editor performance audit\"}"
}
```

5. Запиши це в `request.json` і тримай Editor відкритим. Driver відмовляється від unsaved scene, використовує native `TestRunnerApi` та ізолює product/save identity. `suite` допускає `performance` (повна матриця, timeout 600 с), `roadmap` (детальний замір мапи, timeout 180 с), `foliage` (п'ять regression tests). Не використовуй fixture для свого звичайного save.
6. Дочекайся `finished.txt`, `results.xml`, **виходу з Play Mode та `restored.txt`**. Відновлення identity навмисне відкладене після cleanup: scene `OnDestroy` ще може записувати QA save. `Skipped` або timeout не є Passed. Перевір фактичні test names/count у XML.
7. Повторно звір saves, ProjectSettings, початкові dirty inputs і ADB identity/preferences. Driver відновлює лише власний product-name рядок; hashes підтверджують решту. При невідповідності спочатку досліди запис, не перезаписуй сторонні зміни.
8. Прибери тільки свій injected driver і його `.meta`; порожню створену тобою Editor folder/meta можна також прибрати. Source driver залишається в `tools/qa`. Приватні QA backups не коміть.

Driver не має shell/process/ADB calls і не змінює shared Unity preferences. Performance fixtures перевіряють QA identity до завантаження Boot; foliage regression suite також має guard при активному performance driver.

## Результати

У `QuietCamp/TestResults/Performance2026-10-08/` повна матриця пише `editor-audit.json`, додаткова мапа — `editor-roadmap-audit.json`. Назва робочої папки походить від початку audit; публічні дані мають дату локального звіту **2026-10-09** і точний UTC timestamp усередині.

```bash
python3 tools/analyze_performance.py /path/to/editor-audit.json /tmp/qc-performance-summary
# Optional standalone plots; requires matplotlib:
python3 tools/analyze_performance.py /path/to/editor-audit.json /tmp/qc-performance-summary --plots
```

`summary.json` містить p50/p95/p99/max кадрів, health counters, inclusive method scopes, route operations і object snapshots. `frames.csv` дає wall timeline; `operations.csv` — окремі завершені переходи. Аналізатор також читає `.json.gz`. Scans об'єктів позначені діагностикою і виключені з whole-frame statistics; scoped game calls у цих кадрах зберігаються.

GPU/allocation/Editor counters, що повернули тільки нулі, позначаються недоступними. Wall interval включає Editor/ОС. Method spans вкладені; сумувати їх не можна. Synthetic QA progress, короткі steady windows і Editor rendering не доводять phone FPS, memory leak, production purchases або повне покриття всіх рівнів.
