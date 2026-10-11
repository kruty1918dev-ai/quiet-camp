# Контракти інструментів оточення

Це опис реалізованого API та його меж. Вісім Core-контрактів і спільні
синтетичні приклади перевіряються без Unity. Перевірка compiler references
не замінює native import, EditMode виконання та художнього приймання гри.
Роадмапа залишається архівом; переробка стосується gameplay QC001–QC010.

## Спільна модель роботи

Дані входять у функцію явно. Core не читає сцени, файли, Resources чи
AssetDatabase. Повернений результат можна передати власному генератору,
експортувати у свій формат або створити через Unity-адаптер. Editor Batch
читає recipe лише при прямому виклику. Окремих вікон і прихованого Update немає.

Спеціалізовані пакети залежать лише від scene-placement. Їхні Core assemblies
мають noEngineReferences; Unity/Editor — окремі assemblies. Core не прив’язаний
до renderer, шейдера, українських моделей, сюжетних IDs або правил пазлів.

У генераторів доріг, огорож, мережі, каміння й рослинності вісь Y направлена
вгору, XZ — площина землі. Усі bounds, contacts, sockets, маршрути й параметри
в одній системі координат та одиниці. Сам PlacementContext підтримує власний
Up і власний ISurfaceQuery. PlaneSurface — загальна площина; HeightFieldSurface
та TriangleSurface — Y-up. TriangleSurface вибирає найвищу поверхню; це не
модель багатоповерхової споруди або довільний ray tracer.

Вхідний MeshData зберігає positions, normals, UV0, vertex colors, material
slots на трикутник, додаткові числові channels і semantic parts. Оригінал не
редагується. GeometryBuilder.Append переносить channels і parts; channels,
відсутні в іншій частині, доповнюються нулями. Сумісність значень нуля
визначає адаптер. UnityBridge прив’язує uv0–uv7; інші channels потребують
явного customWriter, інакше повертає помилку, а не губить дані мовчки.

CancellationToken перериває операцію. Групові генератори відкочують власні
reservations. Помилки забороняють bake. Для optional рослин або малих каменів
відхилення повертаються як діагностика й не замінюються випадковими об’єктами.

## scene-placement

**Навіщо:** одна система опори й зайнятого простору для всіх джерел оточення.
Її використовують і авторська сцена, і зовнішній procedural generator.

**Виклик:** PlacementSolver.Place(request, context, cancellation) або
PlacementSolver.Ensemble(requests, context, cancellation).

AssetShape містить ID, локальні contacts, один або кілька Box volumes та
motionMargin. PlacementRequest задає owner, target, yaw, scale, settlement,
maxSlope, maxSupportGap, searchRadius, candidates, seed, required і allowedContacts.
Контакти повинні представляти реальні ніжки/корінь/основу; bbox не є їх заміною.
FromMesh дає простий bounding-box profile, який треба уточнити для складної опори.

Спочатку обирається поверхня й орієнтація. Всі contacts семплюються окремо;
вертикальний зсув не дозволяє одному контакту висіти понад maxSupportGap.
Кінцеві volumes включають масштаб, поворот і motionMargin. XZ spatial hash
шукає сусідів, остаточна перевірка використовує 15 осей OBB SAT. Підключені
IPlacementRule можуть додати правила чужої гри. Пошук має не більше 4096
кандидатів; точне розміщення не пересувається автоматично.

Ensemble має stable IDs, сортування та повний rollback при помилці обов’язкового
члена. allowedContacts дозволяє стики конструкції лише з названими ID.
Під кроною можна розмістити траву: trunk і crown задаються окремими висотними
volumes. Не можна описувати ціле дерево суцільним bbox й очікувати підлісок.

**У QC:** опори лавок/навісів, стовбури, двори й проходи. **Інші ігри:**
будівлі, меблі, предмети й поселення. **Доказ Core:** чотири ніжки на схилі,
повернутий сусід через межу cells, atomic ensemble, cancellation, mesh surface.

## mesh-style

**Навіщо:** узгодити деталізацію різних моделей із художнім референсом,
зберігши конструкцію й впізнаваність. Це окрема редакція геометрії.

**Виклик:** MeshHarmonizer.Harmonize(mesh, profile, projectedArea, cancellation).
StyleProfile задає referenceTriangles/referenceArea, areaExponent, worldScale,
minimum/maximumTriangles, maximumWorldError, maximumFaceTurn, UV/color limits,
NormalMode, protectedVolumes і робочі ліміти. Площу кадру передає caller;
MeshStyleAdapter.ProjectedArea використовує найбільшу площу bounds серед
явно заданих контрольних камер. worldScale також передається явно.

Target обчислюється від площі кадру; менший предмет не успадковує бюджет
великого дерева. Edge collapse перевіряє link condition, захищені вершини,
межі матеріалів, атрибути, поворот граней і накопичену world error. Перевіряються
тільки суміжні грані через індекс incident triangles. Cancellation перевіряється
в циклі кандидатів; maximumCandidateTests обмежує загальну роботу.

Preserve/Smooth/Flat керує нормалями. Parts із preserve захищають опори,
отвори чи впізнавані деталі. Output містить mesh, target, collapsedEdges,
testedEdges, maximumDeviation та діагностику. protected-detail-limit означає,
що target недосяжний без втрати ознак; below-reference-detail не додає вигаданих
отворів або деталей. Оцінка площі bbox не доводить художню якість силуету.

**У QC:** криниця, лавка, ліхтарі й змішані дерева. **Інші ігри:** змішані
asset packs, фіксовані камери, власні стилі. **Доказ Core:** реальне спрощення
closed mesh, protected parts, UV/channels/materials, незмінний source.

## model-weathering

**Навіщо:** керовані ознаки використання й віку з конструктивною логікою.

**Виклик:** Weathering.Apply(id, mesh, profile, cancellation). MaterialWeathering
задає kind, warp, sag, chip, fade, soil, aged і soilTint. WeatheringProfile —
intensity, moisture, seed, damageProbability, rootLock, maximumRemovedParts,
fallback, materials, removableRoles і emitGrowth.

Semantic parts описують support, board, rail, roof-panel та матеріал. Preserve
захищає опору, а спільні геометричні стики блокуються. Вершини на UV/normal seams
отримують однаковий позиційний зсув, тому шви не розходяться. Матеріальні
профілі керують ступенем деформації та кольором; surface age/soil masks
повертаються окремо. Видалення цілого part дозволене лише розміченим ролям,
без втрати protected triangles і всієї моделі. Без розмітки повертається
no-structural-parts і виконується тільки підтримуване поверхневе старіння.

Output — окремий mesh, masks, operations, diagnostics і GrowthRequest[] для
будь-якої зовнішньої системи зелені. Geometry повинна пройти placement вже
після старіння. Це не симулятор руйнування, корозії або фізики уламків.

**У QC:** лавка, навіс, огорожі, опори й криниця. **Інші ігри:** використані
предмети, історичні споруди, занедбані поселення. **Доказ Core:** intensity=0,
protected supports, marked removal, seam-safe displacements, deterministic masks.

## fence-authoring

**Навіщо:** зв’язні огорожі, перила й бар’єри з воротами та кінцевими опорами.

**Виклик:** FenceGenerator.Generate(spec, kit, context, cancellation). FenceSpec
задає contour, closed, openings з arc intervals, seed, jitter, lean та offset.
FenceKit — post/section/gate AssetShape, railSocket у post-local units,
sectionLength, min/maxStretch. Section/gate центрований у нулі й іде вздовж X.

Контур розбивається в кутах, отворах і за довжиною секції. Ставляться кінцеві,
кутові й проміжні posts. Нахил post повторно перевіряє contacts і volumes.
Секція з’єднує реальні post sockets, враховує різницю висот, дозволене розтягування
й названі сусідні стики. Відкритий розрив є явним opening; gate створюється
між двома опорами. Незамкнений або занадто короткий набір не публікується.
Результат: instances, connections і diagnostics; повний rollback при помилці.

**У QC:** джерело/двір/хвіртки. **Інші ігри:** ферми, перила, бар’єри.
**Доказ Core:** відкритий, замкнений і похилий контур, ворота, blocked rollback.

## road-authoring

**Навіщо:** зв’язні маршрути на поверхні з природними переходами покриття.

**Виклик:** RoadGenerator.Generate(spec, surface, cancellation). RoadSpec містить
points, smooth/linear, step, SurfaceBand[] із distance/width/tint/type, edgeRoughness,
heightOffset, potholes/radius, maximumSlope/GradeChange та робочі ліміти.

Polyline або cubic Hermite семплюється за step; центр і краї прив’язуються
до поверхні. Width/color bands плавно інтерполюються за arc distance.
Нерівні краї детерміновані. Вибоїна відкриває локальні трикутники покриття,
а не видаляє весь поперечний відрізок. Інструмент не заглиблює Terrain мовчки:
неприйнятний grade повертає TerrainAdjustment. Самоперетин відхиляється;
перехрестя описуються окремими з’єднаними маршрутами.

Output: mesh, RoutePath, widths, surfaces, materialWeights, corridor,
terrainAdjustments, diagnostics. ValidateConnections перевіряє названі
продовження. Складні довільні intersection polygons і фізичний erosion solver
не входять у цю версію. Адаптер має авторити зону стику маршрутів явно.

**У QC:** дві стежки, стара дорога, входи, роздоріжжя. **Інші ігри:**
пішохідні й сільські маршрути, procedural settlements. **Доказ Core:** опора,
width transition, self-intersection, connected endpoints, explicit terrain requests.

## power-lines

**Навіщо:** мережа опор і підвішених ліній, де всі кінці мають визначений стан.

**Виклик:** PowerGenerator.Generate(spec, kit, context, cancellation). PowerSpec —
RoutePath, offset, min/maxSpan, scale, sag, radius, samples/sides, seed,
fallenSupports і CableDamage[]. SupportKit — standing/fallen shapes, sockets,
networkKind. Fallen asset має вже покладену геометрію та власні ground contacts.

Опори семплюються вздовж маршруту. Intact лінія з’єднує sockets й має
endpoint-preserving sag curve. Це статична художня крива, не фізична catenary
симуляція. Severed дає два вільні кінці; HangingFrom/To має один anchor;
Grounded семплює землю. Fallen pole не може бути standing anchor. Дроти мають
власні volumes; перевіряються контакт із землею та unrelated geometry.
Граф, supports, spans/meshes/volumes і damage states повертаються як дані.
Симуляція електрики та інтерактивний обрив залишаються системі гри.

**У QC:** сільська мережа біля QC006. **Інші ігри:** телеграф, кабелі, троси.
**Доказ Core:** sockets, sag, severed ends, fallen rejection, grounded contact.

## rock-placement

**Навіщо:** природні групи каміння, пов’язані з рельєфом і геологічним профілем.

**Виклик:** RockGenerator.Generate(spec, context, cancellation). RockGroup задає
center, axis, radius, count, min/maxScale та buriedFraction. RockSpec задає
власні shapes, seed і maximumSlope; геологічні zones готує caller.

Головний камінь обов’язковий; супутні менші, розміщуються в анізотропній групі
вздовж axis. Орієнтація враховує surface normal; settlement заглиблює основу.
Placement перевіряє проходи, споруди й сусідів. Відхилені optional камені
отримують діагностику; failure головного каменя відкочує весь запит.
Output — instances/volumes/diagnostics. Це не геологічна симуляція: природний
сенс груп визначає надана карта схилів/берегів/промоїн.

**У QC:** джерело й промоїни. **Інші ігри:** береги, скелі, гірські шляхи.
**Доказ Core:** контакт, burial, slope orientation, blocked hero й determinism.

## vegetation-placement

**Навіщо:** кілька ярусів зелені, пов’язаних із умовами й композицією.

**Виклик:** VegetationGenerator.Generate(spec, context, fields, cancellation).
Species задає shape, tier, weight, scale range, moisture/light ranges,
maximumSlope та spacing. IEnvironmentalFields надає moisture, light, density;
він може читати карту гри, ecology simulation або прості константи. Spec —
source zone, cellSize, density, patchScale/Contrast, seed, candidate limit,
forbiddenSurfaceTags та optional outputBounds.

Кандидати мають global cell IDs і локальний seed; patches залежать від
середовища. Великі/пріоритетні яруси обробляються першими. Spacing використовує
просторовий індекс окремо для ярусів; root й кінцеві wind volumes перевіряє
placement. Будівлі, вода, настили й коридори мають бути передані як surface
tags/reservations. Не можна перевіряти тільки pivot або ігнорувати амплітуду вітру.

Source zone обчислюється цілком; outputBounds лише відбирає частину готових
рішень. Для streaming треба запекти source один раз і розділити outputs,
а не генерувати той самий zone при кожному русі камери. Повторний виклик із
попередніми reservations є новою операцією; для порівняння частин потрібен
однаковий початковий context. Output — instances, acceptedTotal, evaluated,
diagnostics. Сезонні матеріали та анімацію прив’язує адаптер до species/roots.

**У QC:** різні галявини, підлісок, ромашки й сад. **Інші ігри:** ліси,
болота, парки, сади. **Доказ Core:** environmental ranges, water/building
exclusion, partition invariance, deterministic placement й cancellation.

## Bake, кеш і відтворення

Editor-команди читають --authoring-request і --authoring-output. Оригінальні
assets імпортуються read-only через StaticMeshSnapshot; Read/Write importer
не перемикається. Baked outputs містять result.json, recipe.json, native mesh
assets та receipt.json. Hash включає recipe, output, source geometry для
зовнішніх mesh assets і код/manifest пакетів. Незавершена staging folder не
замінює immutable output; move публікує готову папку. Повторне використання
перевіряє result hash. Native metadata й runtime bindings повинні пройти
окрему перевірку в Editor; compiler check цього не доводить.

У Quiet Camp палітра, українські моделі, сюжетні пояснення, gameplay shadows,
сезони й правила навчання залишаються в адаптері. Ці дані не переносяться
в приватні універсальні репозиторії. В embedded-копіях VENDORED.json фіксує
точний upstream commit; жодні vendor models/photos до repo інструментів не входять.
