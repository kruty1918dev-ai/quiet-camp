# Кадри для GitHub-довідника

`GithubDocumentationCaptureTests` знімає справжній Game View: Boot, головне меню, налаштування та privacy-підрозділи, ресурси, мапу/подорожі/bonus, gameplay/HUD, паузу, легенду побажань, завершення й альбом; seasonal tablet frames окремо. Кожен PNG має JSON sidecar з часом, viewport, camera, quality, source context і позначенням synthetic QA progress. Context `capturePhase` може бути `full` (default), `remaining`, `camps` або `context`; останній знімає підтвердження й seasonal AlbumQuiet через справжній diorama runtime.

Player build не потрібний. Не запускай новий Editor і не перезапускай відкритий Editor біля phone session без погодженого безпечного arrangement. ADB preferences і процеси цей fixture не змінює.

Для вже відкритого Editor у дозволеній сесії:

1. Перед capture зроби локальний приватний backup основного save slot та його `.bak`, зафіксуй SHA-256, dirty inputs, product name й ADB preferences/process identity. Не публікуй player data. Збережи свою сцену; Editor має бути поза Play Mode. Потрібна сумісна compilation, реальні Game View/shaders.
2. Використовуй окремий QA project/product; `Application.productName` мусить містити `DocsQA`. У поточному Editor `tools/qa/DocsCaptureDriver.cs` може тимчасово встановити distinct QA product **перед** входом у Play Mode, а відновити його тільки після `RunFinished`, повного виходу з Play Mode й cleanup delay. Передчасне відновлення product identity може направити фінальний QA write у основний save slot. Тести ніколи не мають використовувати player saves.
3. Тимчасово скопіюй driver у `Assets/Editor/`. Trigger — `request.json` у `Path.GetTempPath()/quietcamp-docs-capture-control`; fields: `productName` (prefix `QuietCampDocsQA`, свіжа назва) і `context` (JSON string з runtime source commit, fixture commit і SHA-256 реальних dirty inputs). Не перезаписуй запущений request іншої сесії.
4. Driver запускає тільки `QuietCamp.Tests.GithubDocumentationCaptureTests.CaptureMenusPanelsAndSeasonalCamps`. PNG/sidecars з'являються в `Screenshots/Documentation2026-10-08`, результат у control `results.xml`.
5. Дочекайся виходу з Play Mode та `restored.txt`. Перевір original product, незмінність primary/backup game save slots і попередніх dirty-файлів; прибери лише свої injected fixtures. Driver ніколи не закриває Editor.
6. Оглянь кадри перед публікацією. Перевір resolution, missing-shader/magenta/empty-frame guards і справжню відповідність панелі. Кадри — QA із synthetic progress; вони не доводять device FPS, store/ad integration або visual acceptance окремої незапеченої roadmap-гілки.

Не змінюй generated meshes/catalog заради документації. Для new semantic roadmap спочатку потрібні її native bake/acceptance; кадри main не є рендерами нової композиції.
