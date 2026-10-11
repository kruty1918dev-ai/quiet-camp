# fence-authoring: API та контракти


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


## Мінімальний приклад

```csharp
using Kruty1918.ScenePlacement;
namespace Kruty1918.FenceAuthoring.Samples
{
    public static class Example
    {
        public static FenceKit Kit()=>new FenceKit{post=new AssetShape{id="post",contacts=new[]{V3.Zero},volumes=new[]{new Box(new V3(0,.5f,0),new V3(.06f,.5f,.06f))}},section=new AssetShape{id="rail",contacts=new[]{V3.Zero},volumes=new[]{new Box(V3.Zero,new V3(.75f,.18f,.035f))}},gate=new AssetShape{id="gate",contacts=new[]{V3.Zero},volumes=new[]{new Box(V3.Zero,new V3(.75f,.25f,.04f))}}};
        public static FenceResult Run()=>FenceGenerator.Generate(new FenceSpec{id="garden",contour=new[]{V3.Zero,new V3(6,0,0)},jitter=0,lean=0,openings=new[]{new FenceOpening{id="entrance",from=1.5f,to=3,gate=true}}},Kit(),new PlacementContext(new PlaneSurface()));
    }
}
```

Native tests/import ще Pending. Core-contract success не доводить visual acceptance.
