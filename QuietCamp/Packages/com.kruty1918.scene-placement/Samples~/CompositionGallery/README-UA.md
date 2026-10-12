# Інший host без QuietCamp і Unity

Це виконуваний .NET 8 host тих самих трьох пакетів. JSON описує **форми
асетів і правила**, без координат об'єктів сцени. `riverside-garden.json`
використовує рослинні assets; `cooling-courtyard.json` — колони, сервісний
перехід, резервуари та дрібні промислові акценти біля каналу. Їхні `AssetShape`
є простими тестовими габаритами; це не художні меші або native рендери.

З кореня scene-placement, із сусідніми vegetation-placement і rock-placement:

```sh
dotnet run --project Samples~/CompositionGallery/CompositionGallery.csproj -- \
  --profile Samples~/CompositionGallery/riverside-garden.json \
  --width 6 --depth 8 --seed 12007 --output .verification-core/garden.json
```

Змініть profile на `cooling-courtyard.json` і розміри на 8×10. Рушій і порядок
систем не змінюються. Для іншого розташування repos задайте `-p:VegetationRoot`
та `-p:RockRoot`. Вбудовані UPM packages також підтримуються.

Artifact містить прийняті пози, derived seed, усі спроби/оцінки, groundcover
і hashes профілю та виконуваної assembly. При однакових input/assembly він
відтворюється побайтово. Жоден source QuietCamp або UnityEngine не компілюється.
Візуалізацію створює host adapter власними моделями й матеріалами.

Приклад доводить заміну asset IDs, габаритів та ecological/quality rules.
Він зберігає топологію «прямокутна гра + бічний канал»; це не доказ підтримки
довільних печер, perspective worlds або універсального генератора всіх жанрів.
