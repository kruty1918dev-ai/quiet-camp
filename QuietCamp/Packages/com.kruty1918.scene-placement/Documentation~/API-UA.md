# scene-placement: API та контракти


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


## Мінімальний приклад

```csharp
using Kruty1918.ScenePlacement;
namespace Kruty1918.ScenePlacement.Samples
{
    public static class Example
    {
        public static AssetShape Shape(string id="prop",float height=1,float half=.2f)=>new AssetShape{id=id,contacts=new[]{V3.Zero},volumes=new[]{new Box(new V3(0,height*.5f,0),new V3(half,height*.5f,half))}};
        public static PlacementResult Run()=>PlacementSolver.Ensemble(new[]{new PlacementRequest{id="yard/bench",owner="yard",shape=Shape(),target=new V3(2,0,2)}},new PlacementContext(new PlaneSurface()));
    }
}
```

Native tests/import ще Pending. Core-contract success не доводить visual acceptance.
