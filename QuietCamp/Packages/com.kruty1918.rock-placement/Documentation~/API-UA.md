# rock-placement: API та контракти


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


## Мінімальний приклад

```csharp
using Kruty1918.ScenePlacement;
namespace Kruty1918.RockPlacement.Samples
{
    public static class Example
    {
        public static RockSpec Spec()=>new RockSpec{id="bank",seed=42,rocks=new[]{new AssetShape{id="stone",contacts=new[]{V3.Zero},volumes=new[]{new Box(new V3(0,.2f,0),new V3(.25f,.2f,.2f))}}},groups=new[]{new RockGroup{id="outcrop",center=V3.Zero,count=6,radius=2}}};
        public static RockResult Run()=>RockGenerator.Generate(Spec(),new PlacementContext(new PlaneSurface()));
    }
}
```

Native tests/import ще Pending. Core-contract success не доводить visual acceptance.
