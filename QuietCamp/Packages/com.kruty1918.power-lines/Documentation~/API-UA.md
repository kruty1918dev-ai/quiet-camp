# power-lines: API та контракти


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


## Мінімальний приклад

```csharp
using Kruty1918.ScenePlacement;
namespace Kruty1918.PowerLines.Samples
{
    public static class Example
    {
        public static SupportKit Kit()=>new SupportKit{standing=new AssetShape{id="wood-pole",contacts=new[]{V3.Zero},volumes=new[]{new Box(new V3(0,1.5f,0),new V3(.07f,1.5f,.07f))}},fallen=new AssetShape{id="broken-pole",contacts=new[]{V3.Zero},volumes=new[]{new Box(new V3(0,.07f,0),new V3(1.5f,.07f,.07f))}},sockets=new[]{new V3(-.22f,2.8f,0),new V3(.22f,2.8f,0)}};
        public static PowerSpec Spec()=>new PowerSpec{id="village-line",route=new RoutePath(new[]{V3.Zero,new V3(0,0,16)}),sag=.4f};
        public static PowerResult Run()=>PowerGenerator.Generate(Spec(),Kit(),new PlacementContext(new PlaneSurface()));
    }
}
```

Native tests/import ще Pending. Core-contract success не доводить visual acceptance.
