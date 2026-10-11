# Роадмапу відкладено — 11.10.2026

За рішенням користувача роадмапа збережена для майбутньої роботи й виключена з player. Художній прохід ще не прийнятий, оптимізаційний етап не завершений. Історичні кадри й результати зберігають свої дати.

«Почати/Продовжити» відкриває незавершений доступний пазл або наступний рівень головної кампанії. Після перемоги «Наступний рівень» відкриває наступний доступний пазл поточної подорожі; наприкінці подорожі кнопка повертає в меню. Вихід із gameplay повертає в головне меню. Картку маршруту прибрано, старий pending destination `Levels` перенаправляється на `Main`. Основна кампанія знову використовує весь чинний порядок, без обмеження на п'ять місць. Прогрес, альбом, пазли й права доступу збережені.

## Що збережено

- [Авторинг долини](../../../../QuietCamp/Assets/QuietCamp/Authoring/Roadmap/Composition/CinematicPilot): рельєф, маршрути, ансамблі, матеріальні прив'язки й останній незавершений прохід зелені.
- [Editor-архів](../../../../QuietCamp/Assets/QuietCamp/Editor/RoadmapArchive): усі ревізії запеченої геометрії, World.asset, матеріали, текстури, власні shaders, культурні моделі, старі каталоги й stylesheet мапи. Перенесено 134 файли, **243 770 999 байтів вихідних ресурсів**; GUID збережені. Це не вимір зменшення стиснутого APK/AAB.
- Renderer, camera input, world/chunk types і завантажувач архіву компілюються лише з `UNITY_EDITOR`. Спільні моделі й сюжетні деталі, потрібні gameplay, залишаються в runtime.
- Native bake/capture працюють із новим Editor-архівом. Для ручного перегляду: увійти в Play Mode у MainMenu → `Quiet Camp / Cinematic Roadmap / Open Archived Preview`. Звичайне проходження в Editor також обходить мапу. Автоматичний старий тест пілота позначено Explicit і не запускається зі звичайними тестами апки.

## Перевірки

[Manifest перенесення](archive-manifest.json) містить шлях, розмір і SHA-256 кожного збереженого файлу. [Статична перевірка](verification-receipt.json) звіряє ці хеші, виключення Resources і незмінність сторонньої `QC_Renderer.asset`.

`DeferredRoadmapBuildGuard` перед майбутнім дозволеним білдом перевіряє залежності enabled scenes і всіх runtime Resources. Якщо вони посилаються на архів або map-only assets знову опинилися в Resources, білд зупиняється. Перевірка доступна окремою командою Editor без BuildPipeline.

Команди без player build:

```sh
python3 tools/verify_deferred_roadmap.py
python3 tools/prepare_ui_styles.py --check
python3 tools/verify_monetization.py --include-editor
python3 tools/verify_monetization.py --player-source-check
dotnet build tools/MonetizationProbe.csproj -m:1 -p:UseSharedCompilation=false
dotnet run --no-build --project tools/MonetizationProbe.csproj
```

Compiler check з вимкненим UNITY_EDITOR додатково перевіряє відсутність типів native роадмапи в отриманих assemblies. Це перевірка вихідного коду з кешованими Unity references; вона не створює player і не вимірює розмір встановленої апки.

Native інтеграційний тест `DeferredRoadmapPlayModeTests` призначений лише для погодженого ізольованого QA Editor. Він перевіряє imported dependency graph, збережений світ, відсутність мапи в Resources, прямий старт/Next/повернення, QC005 → QC006 і старий прогрес. Його результат вважається підтвердженим лише за наявності Passed XML та актуального `native-flow-receipt.json`.

Поточний результат native прогона: dependency graph і прямий старт перевірені; повний сценарій ще не Passed. Перший прогін натиснув Next до завершення layout, другий викликав повернення до завершення transition. Очікування готового кадру й завершення transition виправлене у тесті; третій Editor впав у FMOD до результату. Статичний guard, compiler checks і domain contracts пройшли. Player builds не запускалися.
