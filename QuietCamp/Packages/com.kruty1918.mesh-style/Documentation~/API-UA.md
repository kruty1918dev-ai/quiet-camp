# mesh-style: API та контракти


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


## Мінімальний приклад

```csharp
using Kruty1918.ScenePlacement;
namespace Kruty1918.MeshStyle.Samples
{
    public static class Example
    {
        public static MeshData Model(){var b=new GeometryBuilder();b.Box(new V3(0,.5f,0),new V3(1,1,1),Tint.White);return b.Build();}
        public static StyleResult Run()=>MeshHarmonizer.Harmonize(Model(),new StyleProfile{referenceTriangles=12,referenceArea=10000},10000);
    }
}
```

Native tests/import ще Pending. Core-contract success не доводить visual acceptance.
