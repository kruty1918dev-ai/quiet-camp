# model-weathering: API та контракти


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


## Мінімальний приклад

```csharp
using System.Linq;
using Kruty1918.ScenePlacement;
namespace Kruty1918.ModelWeathering.Samples
{
    public static class Example
    {
        public static MeshData Model(){var b=new GeometryBuilder();b.Box(new V3(0,.5f,0),new V3(1,1,1),Tint.White);var m=b.Build();m.parts=new[]{new MeshPart{id="support",role="support",preserve=true,triangles=Enumerable.Range(0,6).ToArray()},new MeshPart{id="panel",role="board",materialKind="wood",triangles=Enumerable.Range(6,6).ToArray()}};return m;}
        public static WeatheringResult Run()=>Weathering.Apply("abandoned-bench",Model(),new WeatheringProfile{seed=42,intensity=.65f});
    }
}
```

Native tests/import ще Pending. Core-contract success не доводить visual acceptance.
