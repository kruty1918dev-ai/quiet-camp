# Продовження composition handoff — 2026-10-08

Гілка `art/roadmap-native-finish-2026-10-08`, база model-kit `46c75ae` з checkpoint `bafdaa4`. Архів перевірено: 5297 payload-файлів збігаються за SHA-256. Основний checkout і його dirty-файли не змінені.

Чотири справжні UPM пакети включено всередину `Packages/`, зі збереженими GUIDs, ліцензіями й upstream revisions. Portable pipeline використовує офіційний Newtonsoft.Json 13.0.3 через NuGet, без залежності від Unity Library; Unity pins не змінені. Вихідні артефакти CLI локальні для checkout, без спільного `/tmp` між worktrees.

SDK 10.0.401 і Python 3.11 із requirements встановлено в ізольовану tooling-папку. Portable build пройшов без warnings/errors. Новий Unity Editor не запускався: погодження безпечного ADB arrangement ще немає. Native catalog залишається legacy; нові рендери й native acceptance не заявляються.

GitHub push заблокований відсутністю HTTPS credentials; commits локальні. Merge в main не виконаний.

## Перші 10 і portable semantics

Намети прибрано з генератора roadmap, revealed props, branch props, pending і unknown silhouettes. Gameplay-код/пазли/сейви не редагуються. Region 0 має одну лісничу ділянку; зайві садиби першої десятки й field zone прибрані. Region 1 зберігає садибу біля рівня 14; перші 10 отримали щільні forest zones. Канон QC007 left / QC010 right підтверджено в summaries; channel recipes відповідають side/offset/width, power corridor обійшов берег.

Додано `SpanDamage` з severed/hanging-from/hanging-to/removed, індексовані fallen supports, заборону live wires на fallen geometry, перевірки індексів і drop length. Renderer використовує socket metadata обох опор, однаковий pivot/source-height transform. Clearance опор тепер враховує фактичну фінальну висоту, а не вихідний розмір.

`SurfaceRecipe` описує convex source polygons для gravel, bus-bay, pothole, garden, reservoir-bed, channel, erosion, service-yard. Перевіряються форма/ownership/сухий обхід/опора на берег. Native baker враховує sources у hashes, кліпує surface geometry по chunk boundaries; wet patches виключають рослинність, очерет/верби дозволені тільки біля їхніх берегів. Catalog ще не опублікований.

На цьому host різниця libm призводила до `-0.0` у старих exporter outputs. Canonical zero formatting у власному generator усуває цю різницю; відповідні owner exports, provenance hashes і matrix регенеровано. Форму моделей, GUIDs і triangle counts збережено: 24 original EnvironmentKit / 4208 triangles, ship LOD 1100/650/300.

Перевірки: .NET build і semantic contracts пройшли, CLI negative/atomic tests пройшли. Current-source compilation усіх п'яти package runtime assemblies, чотирьох шарів гри, Editor/PlayMode test sources і QuietCamp.Editor пройшла проти наявних Unity references. Є compiler/analyzer warnings; native import/shader/scene tests не виконані.
