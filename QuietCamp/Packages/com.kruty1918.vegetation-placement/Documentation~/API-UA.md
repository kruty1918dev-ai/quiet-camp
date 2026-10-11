# vegetation-placement: API та контракти


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


## Мінімальний приклад

```csharp
using Kruty1918.ScenePlacement;
namespace Kruty1918.VegetationPlacement.Samples
{
    public static class Example
    {
        public static VegetationSpec Spec()=>new VegetationSpec{id="meadow",seed=42,zone=new Box(V3.Zero,new V3(4,1,4)),density=1,patchContrast=0,species=new[]{new Species{id="grass",spacing=.15f,shape=new AssetShape{id="grass-mesh",contacts=new[]{V3.Zero},volumes=new[]{new Box(new V3(0,.1f,0),new V3(.05f,.1f,.05f))}}}}};
        public static VegetationResult Run()=>VegetationGenerator.Generate(Spec(),new PlacementContext(new PlaneSurface()),new ConstantFields());
    }
}
```

Native tests/import ще Pending. Core-contract success не доводить visual acceptance.
