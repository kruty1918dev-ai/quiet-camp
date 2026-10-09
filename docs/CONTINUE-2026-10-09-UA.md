# План продовження після переїзду · Quiet Camp

[Звіт і пакет](TRANSFER-2026-10-09-UA.md) · [Карта продуктивності](../PERFORMANCE_MAP.md)

## 1. Отримати й перевірити матеріали

Увійти як `kruty1918dev-ai`, відкрити [приватний release](https://github.com/kruty1918dev-ai/quiet-camp-transfer-2026-10-09/releases/tag/transfer-2026-10-09), завантажити `QuietCamp_Transfer_2026-10-09.zip` та `.sha256`. Перевірити контрольну суму, розпакувати в короткий локальний шлях із достатнім вільним місцем. У папці `QuietCamp-Transfer` запустити:

```sh
python verify_transfer.py
```

Ця команда звіряє payload SHA-256, containment local UPM packages, pins і case collisions без запуску Unity. Відкрити `SOURCE-STATES.json`, цей план та звіт. Не накладати `prepared-roadmap/` поверх `current-project/`: це різні гілки з різними renderer/UI контрактами.

Для збереження Git-історії:

```sh
git clone --branch perf/optimize-transitions-2026-10-09 https://github.com/kruty1918dev-ai/quiet-camp.git
cd quiet-camp
git fetch origin art/roadmap-native-finish-2026-10-08
git switch -c art/model-integration-laptop
```

Зіставити source commits із `SOURCE-STATES.json`. ZIP snapshots дозволяють відновити файли без Git/network; їхні SHA не створюють Git-історію самі по собі.

## 2. Підготувати середовище

Unity **6000.6.2f1**, проєкт відкривається через папку `QuietCamp/`. Зберегти package pins; embedded пакети вже включені. Для prepared portable CLI — .NET SDK **10**, Python **3.11+**, окреме середовище з `tools/roadmap-assets/requirements.txt`. Поточний старий MonetizationProbe має залежність від Unity Library; portable prepared версія використовує NuGet й checkout-local outputs. Не переносити стару Library як доказ fresh import.

Перевірити RAM, >10% вільного диска, один Unity job за раз. Якщо є активна телефонна сесія — спочатку забезпечити безпечну ізоляцію Editor від external ADB. Player builds/APK/AAB/інсталяція й активація billing не входять у цей план.

## 3. Об'єднати composition із оптимізаціями

Прочитати обидва `AGENTS.md`, `tools/scene-composition/USAGE_UA.md`, README і diagnostics. Виконати merge `origin/art/roadmap-native-finish-2026-10-08` у новій гілці, вручну розв'язати конфлікти. Зберегти сучасний campaign ID order, save format, monetization behavior, 110 main nodes, GPU leaf shader, caches, bounded UI, stable DOM IDs та staged scene preparation. Handoff тексти про старі 30 main levels — історичний контекст, не дозвіл скоротити поточну кампанію.

Найважливіші конфлікти: `RoadmapGraphic/Glade/Layout/SceneGenerator/Painter/Weather`, `MenuMapBinding/MenuScreens`, `ScreenRouter`, `EnvironmentComposer`, locale dictionaries і roadmap tests. Новий 3D renderer/chunk streaming має зберегти native hit targets і scroll coordinates. Не приймати весь old UI або всю new UI сторону автоматично. Підсумковий campaign snapshot і unlocks порівняти з поточною гілкою. Перевірити template ranges для всіх 110 main IDs: старий authoring не є доказом покриття решти кампанії.

Спершу sequential portable checks: model integrity, deterministic exporters `--check`, inventory `--check`, .NET roadmap pipeline build/contracts, composition validate/compose dry-run та CLI negative/atomic tests. Далі current-source runtime/Editor/test compilation. Зберегти журнали й точний checkpoint, commit/push після coherent verified milestone.

## 4. Native bake і перевірка вигляду

Після успішного import та compilation — `Quiet Camp → Composition → Bake Main`; окремо Bake Staging. Перевірити атомарну публікацію catalog. Native scene/streaming acceptance: `BakeAndValidateMainGallery`, потім `Native360ScrollResizeReentryAndLowMemory`, плюс відповідні forest, foliage та UI regressions. Synthetic QA saves, з відновленням налаштувань і без торкання player slots.

Перші 10 nodes: густий ліс, QC007 water left/QC010 right за actual source, одна доречна forester ділянка, без roadmap tents. Село — за межами forest-only зони: road → gates → enclosed yards → rear gardens → field. Перевірити grounding, закриті огорожі, сухі approaches, conductor sockets/damage, відсутність live wires до fallen tower. Aircraft/dam/ship лишаються staging.

Captures зі штатної камери: portrait **720×1600**, tablet **1280×800**, перша десятка й regional seams, yard/bus stop/power details; окремі aircraft/dam staging кадри. Порівняти з `references/selected/`. Фіксувати resolution, date, source commit/hash, camera, node/chunk, LOD і фактичні metrics. Недоліки виправляти через source owner: inspect → patch --expected-hash → validate → compose; rebake/rerender. Старі PNG та концепти не називати новими рендерами.

## 5. Донори UAC та performance

Комерційні пакети поки окремий private backup. Перед використанням перевірити ліцензії/офіційне отримання; не пушити їхні source assets у публічний quiet-camp. Обрати мінімальні кандидати зі звіту, перевірити meshes/materials/LOD у sandbox, адаптувати палітру й масштаби, імпортувати лише прийняті об'єкти. Власний kit уже закриває основні потрібні ролі.

Повторити leaf/scene/map матрицю після інтеграції, з baseline та source provenance. Перші performance цілі: warm leaf mesh до cover, атмосфера без activation spikes, incremental Levels mount, легші far chunks. Budgets: route кадри ≤100 мс, scroll p95 ≤33,33 мс, steady render спершу ≤33,33 мс; досягнення перевіряти, а не оголошувати за наявністю кешу. Native chunks: ≤3 nearby, ≤3 main nodes/chunk, ≤15 MiB/chunk, shared materials, одна world camera/RT, орієнтир ≤32 world renderers і 64 MiB presentation estimate. Editor counters не доводять device FPS.

## 6. Завершити публікацію

Оновити model matrix та `CAMPAIGN_ROADMAP.md`, screenshots/галерею/Pages лише перевіреними captures. Додати свіжий performance report, native receipts, hashes й перелік залишкових проблем. Commit/push окремої гілки. Main активувати після перевіреного узгодженого результату; не переносити неперевірений catalog заради самого merge. Наприкінці прибрати лише свої QA files, перевірити save/settings/ADB guards і створити наступний portable snapshot.
