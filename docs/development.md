[← Головна](../README.md) · [Довідник](README.md) · [Екрани](screens.md) · [Галерея](gallery.md) · [Розробка](development.md) · [Стан](status.md)

# Відкрити й перевірити проєкт

![Карта залежностей та шарів](images/diagrams/project-map.svg)

## Перший запуск

| Потрібно | Версія / дія |
| --- | --- |
| Unity Editor | **6000.6.2f1**, revision `770e33f6875c`; [ProjectVersion](../QuietCamp/ProjectSettings/ProjectVersion.txt). |
| UPM packages | Exact pins у [manifest](../QuietCamp/Packages/manifest.json) і [lock](../QuietCamp/Packages/packages-lock.json); internet для першого resolve. |
| Custom local packages | Embedded у `QuietCamp/Packages/`; сусідні repositories не потрібні. [Upstream revisions/licenses](../QuietCamp/Packages/README.md). |
| CLI tooling | .NET SDK 10 для probe; Python 3 для lightweight checkers. |
| Android modules | Потрібні для Android development; для відкриття/Editor workflow player build не потрібний. |

З кореня repo виконай `python3 tools/verify_portability.py` (Windows: `py -3 tools/verify_portability.py`). Відкрий **QuietCamp/** у Unity Hub, дочекайся package resolution/import, відкрий [Boot.unity](../QuietCamp/Assets/QuietCamp/Scenes/Boot.unity). Gameplay запускається через Boot: він збирає сервіси, policy flow і навігацію. Не починай з порожньої Camp scene без Bootstrap.

Перший import на іншій ОС/host не доводиться статичною перевіркою. При помилці спочатку звір exact Editor, package identity/pins і першу compiler error; не копіюй чужу Library як постійну залежність проєкту.

## Перевірки

Workspace [AGENTS.md](../AGENTS.md) задає актуальні дозволи й host rules. Зараз player builds/APK/AAB та installation не є routine verification. Біля active phone session навіть Editor launch може вплинути на ADB: використовуй погоджений arrangement; не змінюй shared preferences чи чужі процеси.

Послідовно, з кореня repo:

```sh
python3 tools/verify_portability.py
python3 tools/verify_github_docs.py
dotnet build tools/MonetizationProbe.csproj -m:1 -p:UseSharedCompilation=false
dotnet run --no-build --project tools/MonetizationProbe.csproj
python3 tools/verify_monetization.py
python3 tools/test_legal_site.py
```

`verify_portability` не потребує Library. У main поточний probe також читає cached Newtonsoft reference із Library після Unity import. Cached compiler helper потребує реальних Unity Bee responses/reference assemblies після import; він компілює source, але не виконує scene tests. Probe використовує synthetic temporary saves і fake platform providers; список перевірених puzzles дивись у результаті конкретного запуску, не в розмірі всього content catalog.

## GitHub Pages

Static site живе у `docs/index.html`, `site.css`, `site.js`, `site-content.json` та `site-i18n.json`. Джерело публікації — main `/docs`; `.nojekyll` зберігає site як звичайні static assets. UA/EN/DE перекладають пояснення сайту; native captures гри залишаються українськими.

`python3 tools/verify_github_docs.py` перевіряє links/anchors, усі PNG hashes і metadata та locale coverage. [Browser fixture](../tools/qa/test_github_guide_browser.py) потребує окремого Python Playwright + Chromium середовища; його можна запустити локально або з `--url` для опублікованого сайту. Він перевіряє image loading, пошук/filters, мови, viewer, responsive overflow і fallback без JavaScript. Немає npm install, bundler або external CDN dependency у самому сайті.

## Знімки документації

[Capture workflow](../tools/qa/DOCS-CAPTURE-UA.md) описує guarded `GithubDocumentationCaptureTests`, QA identity та JSON sidecars. Capture потребує rendered Game View, не `-batchmode`. У відкритому Editor можна виконати QA без нового launch; source/settings/player-save hashes перевіряються до й після. Використовуй свіжий QA product і не запускай destructive fixtures на player storage.

[Галерея](gallery.md) зберігає зображення й provenance. [Стан](status.md) відділяє source compilation, native test/capture, player/device checks та staging assets.

## Зміна UI або світу

- Екран: [Атлас](screens.md) → source owner → [HTML UI](../Documentation/HTML-UI.md). Зберігай stable ids, callback routing, safe-area й scroll state.
- Puzzle: редагуй source/content pipeline, перевір правила/solver; не змінюй логічний рівень через decorative props.
- Нова roadmap: працюй із semantic composer в [окремій branch](https://github.com/kruty1918dev-ai/quiet-camp/tree/art/roadmap-native-finish-2026-10-08/tools/scene-composition), inspect owner → guarded patch → validate → dry-run → дозволений native bake/capture.
- Готовий milestone: онови відповідну docs-сторінку, виконай доречну перевірку, commit і push; концепт не замінює Game View evidence.

## Assets і ліцензії

Code/art/audio/vendor sources мають власні license/provenance records; наявність файлу в repository не надає універсального права на redistribution. Kenney/Poly Haven CC0 notes збережені, DOTween/audio/Stylized Water vendor terms залишаються чинними. [Lighting source](../QuietCamp/Assets/QuietCamp/ThirdParty/PolyHavenLighting/SOURCE.md) · [Water integration](../QuietCamp/Assets/ThirdParty/Stylized%20Water%203/QUIET-CAMP-INTEGRATION.md).
