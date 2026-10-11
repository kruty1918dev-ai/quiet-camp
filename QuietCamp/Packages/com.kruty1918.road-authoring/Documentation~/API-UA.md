# road-authoring: API та контракти


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


## Мінімальний приклад

```csharp
using Kruty1918.ScenePlacement;
namespace Kruty1918.RoadAuthoring.Samples
{
    public static class Example
    {
        public static RoadSpec Spec()=>new RoadSpec{id="path",seed=42,points=new[]{V3.Zero,new V3(0,0,4),new V3(2,0,8)},bands=new[]{new SurfaceBand{distance=0,width=.8f},new SurfaceBand{distance=8,width=1.1f}}};
        public static RoadResult Run()=>RoadGenerator.Generate(Spec(),new PlaneSurface());
    }
}
```

Native tests/import ще Pending. Core-contract success не доводить visual acceptance.
