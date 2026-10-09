# Матриця моделей Quiet Camp — 2026-10-08

У каталогах доступні **38** моделі; до цієї зміни вже було **9** неопублікованих donor-варіантів літака/корабля. Додано **24 власні** low poly моделі, включно з LOD: **4 208 трикутників**. Повний список: [model-inventory.csv](model-inventory.csv), [model-inventory.json](model-inventory.json).
Інвентаризовано **81** сирих файлів геометрії: [raw-model-files.csv](raw-model-files.csv). OBJ має точну кількість трикутників після fan-triangulation; GLB — суму triangle primitives; FBX зазначено без неперевіреної кількості. Процедурні story-моделі враховано через їхні mesh streams.

**Нових завантажень зі Sketchfab немає:** вхід зупинився на 2FA, Google-сеанс завершився. Знайдені кандидати наведені нижче. Власний EnvironmentKit не походить від цих моделей і не видається за імпорт. Наявний Sketchfab-корабель перероблено з уже збереженого атрибутованого GLB.

Набір доступний редактору та native baker; main/runtime catalog досі legacy. Native bake, новий рендер, Unity import і мобільна продуктивність не перевірені: AGENTS.md блокує новий запуск Editor поряд з активною ADB-сесією.

## Ціль та межі

Камера залишається далекою, orthographic, pitch 55° / yaw 0° / roll 0°. Прості плоскі меші, наявна палітра, спокійні природні зелені й теплі нейтральні тони; насиченість створюється ансамблями. Занедбаність читається через порожні вікна, отвори в дахах, бур'яни й іржу, без урожаю та суцільної сірості.
Українське прочитання має виникати з поєднання хати, двору, заднього городу, лісосмуг, інфраструктури й стриманого власного орнаменту. Окремий вулик або синя облямівка не гарантує впізнавання країни іноземцем. Прапорів, тризубів, текстових підказок і запозиченої російської атрибутики не додається.

## Наявне → потрібне

| Роль / де | Було | Додано / підготовлено | Дія та логіка розміщення |
|---|---|---|---|
| Густий ліс · 1–10 | `tree_default`, `tree_pineRoundA`, `plant_bushSmall` | `ua_young_willow`, `ua_poplar` | Зберегти наявні крони; щільність створювати групами, просіками й узліссям, а не деталізацією листя. Основні масиви обабіч галявин; береги — верба, дороги/поля — рідкі тополі. |
| Луг і занедбане поле · 1–10 / поле літака | `grass`, `grass_leafsLarge` | `ua_field_weeds`, `ua_field_weeds_lod` | Замінити врожай на нерівномірні низькі бур'яни; не використовувати пшеничні та соняшникові ряди. Відкрита площина за задніми городами або окреме поле з лісосмугою. |
| Берег і очерет · 1–10 / дамба | `branch_pond` | `ua_reed_clump`, `ua_reed_clump_lod`, `ua_young_willow` | Вода/рельєф генеруються географією; pond не замінює потрібне озеро чи річку. Очерет уздовж реальної берегової лінії; не на стежках і не всередині водного проходу. |
| Занедбана українська хата · Рідкі садиби / село | `ua_whitewashed_house` | `ua_abandoned_house` | Порожні вікна, отвір у даху, тепла вицвіла побілка та стримана синя облямівка. Тільки у власній ділянці; фасад до воріт, позаду город і поле. Другого ряду хат за городом немає. |
| Ліснича хатина · Ліс | — | `ua_forester_hut` | Окремий малий дерев'яний об'єм із пошкодженим дахом. Рідкісна службова галявина з підходом; не сусідство з туристичним кемпом або автобусною зупинкою. |
| Сарай · Подвір'я | — | `ua_barn` | 216 трикутників у власній тимчасовій моделі; зовнішній донор очікує завантаження. Бічна частина огородженого двору; ворота й прохід до хати залишаються вільними. |
| Курник · Подвір'я | — | `ua_chicken_coop` | 240 трикутників; опори та пандус торкаються землі. Це власна модель, не копія Sketchfab. У тому самому дворі, збоку від проходу; на дальній мапі не перебільшувати масштаб. |
| Вулики · Подвір'я | — | `ua_beehive` | Простий дерев'яний прямокутний вулик, 96 трикутників; без медозбору. 1–3 вулики вздовж внутрішнього краю ділянки, не на громадській дорозі. |
| Криниця · Подвір'я | `ua_well` | `ua_well_sweep` | Наявна криниця або власний журавель із відкритим кільцем і противагою. У дворі біля доступного проходу; не в городі або на дорозі. |
| Замкнений паркан і ворота · Садиби | `ua_wattle_fence`, `ua_picket_fence`, `ua_concrete_fence`, `ua_gate` | `ua_picket_fence_damaged` | Сегменти належать одному ансамблю; пошкодження дошок не замінює повного периметра ділянки. 360° межа двору й городу, розрив тільки для воріт; у селі сусіди мають спільну межу. |
| Зупинка з культурною деталлю · Громадська дорога | `ua_bus_shelter` | `ua_bus_shelter_mosaic` | Власний геометричний мотив фантастичного птаха/рослини; 197 трикутників. Видимість стін з камери потребує native render. За межами двору, на дорожньому узбіччі з відкритим майданчиком для зупинки автобуса. |
| Сільська електромережа · Село | `ua_rural_pole` | — | Опори й проводи — типізована мережа, а не випадкові прикраси. Уздовж дороги; окремі відводи до садиб. Не з'єднувати напряму з високовольтними проводами. |
| ЛЕП: іржа й повалена опора · Поля / лісосмуги | `ua_power_pylon` | `ua_power_pylon_rusted`, `ua_power_pylon_fallen` | Збережені conductor sockets стоячої опори; повалена — окремий декор без фальшивого живого span. Рідкісні масштабно доречні прольоти; не заповнювати ними весь перший ліс. |
| Провислі / обірвані дроти · Мережі | — | — | Це процедурні криві між sockets. Провисання вже є; стан обриву для окремого прольоту ще потребує реалізації. Кожен цілий провід має дві опори. Обрив прив'язаний до опори/землі; floating wires заборонені. |
| Пішохідний місток · Лісова вода | — | `ua_plank_bridge` | 168 трикутників; дві берегові точки підходу. Положення потребує перевірки рельєфу. Перекриття конкретного вузького струмка; обидві опори на суші, не поперек широкого озера. |
| Розбитий літак · Окрема неопублікована подорож | `staging_aircraft_fore`, `staging_aircraft_tail` | — | Уже є атрибутована цивільна геометрія та LOD. Великі частини впізнавані; це не сертифікована реконструкція Boeing 777. Одне поле з лісосмугою, дві частини фюзеляжу, одна хвостова частина; маршрут обходить уламки. |
| Пошкоджена дамба · Окрема неопублікована подорож | — | `ua_dam_breached`, `ua_dam_breached_lod`, `ua_hydro_service_building` | Власна умовна геометрія з реальним наскрізним розривом; не реконструкція конкретної події. Поперек річки; колишній басейн і сучасне русло різні. Розрив не є прохідним мостом. |
| Судно на колишньому березі · Дамба | `staging_cargo_ship` | `ua_mooring_post` | Наявний Sketchfab-донор перероблено: 1100/650/300 трикутників, спільна палітра та LOD pivot. На колишній верхній береговій смузі, не у вузькому сучасному руслі. |
| Кури / дрібний побут · Локальна сцена / gameplay | `log`, `log_stack`, `stump_round`, `campfire_stones` | `ua_chicken` | Не робити невидимі з висоти предмети центральними елементами roadmap. Кури — тільки у дворі; колоди — службова ділянка або край лісу. |
| Намет · Тільки gameplay | `tent_detailedOpen`, `tent_smallOpen` | — | Модель у проєкті є; для загальної roadmap її не використовувати. Кемпінг позначає місцина, не намет на стежці. Старий renderer ще потребує окремої ревізії силуетів. |
| Дорожня кишеня / ями / задній город · Село | — | — | Потрібні surface/parcel recipes, а не додаткові високополігональні меші. Геометричне покриття моделями не означає завершену композицію. Дорога → ворота → двір/хата → огороджений город → поле; будинки по обидва боки дороги. |

## Авторинг у цій зміні

`ua.abandoned-homestead`: пошкоджена хата повернута фасадом до воріт, сарай/курник/вулик у власному дворі; видалені дерева, які займали місця господарських споруд, додано бур'яни позаду. У roadmap-полях регіонів 0 і 1 врожай замінено на `ua_field_weeds`; це охоплює перші 10 рівнів, але весь регіон 1 простягається до рівня 15. Висота покриву береться з метаданих, не зі значення для соняшника. Зупинка отримала власний мотив на стіні. Непідключений `stone_largeC` у двох шаблонах замінено на реально наявний `stone_largeA`; старий невикористаний metadata-запис залишено для окремого очищення.
Ці зміни рецептури перевіряються inspect → patch з source hash → validate → compose --dry-run. Вони не пересувають вузли, не змінюють puzzle IDs, порядок, unlock rules або save-прогрес. Повне перепланування села, окремий стан обірваних проводів і вилучення старих наметів із roadmap-силуетів залишаються відкритими задачами, а не вигаданою готовністю моделей.

## Sketchfab: джерела та статус

Точні license version, комплект файлів і право на redistribution перевірити після завантаження. CC-BY candidate не означає, що його геометрія вже у Git. Для кожного нового імпорту зберігати автора, URL, ліцензію, SHA-256 сирого файлу та перелік змін. CC-BY-NC не підходить; Free Standard не прирівнювати до CC-BY.

| Кандидат | Автор / ліцензія | Статус | Адаптація |
|---|---|---|---|
| [Chicken Coop](https://sketchfab.com/3d-models/chicken-coop-1f72fd7e01bc4b7bbbad102e623548ef) | synistersyrup · CC-BY-4.0 | awaiting-download | Remove textures, bake shared flat palette, simplify to <=260 triangles, preserve raised legs and grounded ramp. |
| [Rural brick house with barn and chicken coop](https://sketchfab.com/3d-models/rural-brick-house-with-barn-and-chicken-coop-4708a315b2ff42d08c55c6f5d5ca7c21) | Vitalii.Sandula · CC-BY; exact version requires download verification | awaiting-download | Extract barn/coop as separate grounded meshes; no PBR texture set or whole prefab yard. |
| [Lowpoly Shed](https://sketchfab.com/3d-models/lowpoly-shed-c7a5f5f7f15e4225a7e107faf9d44235) | AspectStudios · CC-BY; exact version requires download verification | awaiting-download | Muted wood/roof palette, missing roof strip and empty doorway; <=320 triangles. |
| [Low Poly Barn](https://sketchfab.com/3d-models/low-poly-barn-fce7ec6e083e41d7b6e32f9bfcab6cd0) | PeriltekGames · CC-BY; exact version requires download verification | awaiting-download | Check silhouette against Ukrainian utility shed proportions, remove decorative/PBR detail, <=320 triangles. |
| [Bee Hive](https://sketchfab.com/3d-models/bee-hive-5ac5d5a85d0d41d396565718c0ba7ac3) | Lisiaasty · CC-BY; exact version requires download verification | awaiting-download-and-shape-review | Verify rectangular wooden apiary box rather than a natural hanging hive; <=120 triangles, no active harvest. |
| [Lowpoly Bridge](https://sketchfab.com/3d-models/lowpoly-bridge-2df174f3628f4b31b758e94aa9efaf32) | AspectStudios · CC-BY; exact version requires download verification | awaiting-download | Plain deck <=240 triangles, matching sockets at both banks, no terrain or water in model. |
| [Animated Willow Tree](https://sketchfab.com/3d-models/animated-willow-tree-917d91261019446898a99dd080b2fc88) | lucq22 · CC-BY; exact version requires download verification | awaiting-download-and-shape-review | Remove rig and leaf cards; reduce to 2-3 coarse crown volumes, <=180 triangles plus LOD. |
| [Hydro Power Dam](https://sketchfab.com/3d-models/hydro-power-dam-e46fcc415d6f4ff4b1b9049311a53f56) | shaun.in.3d1 / OpenEnergy3D · Free Standard | not-approved-for-repository-redistribution | Do not import until redistribution permission is verified; use original fictional dam study meanwhile. |
| [Low Poly Cargo Ship](https://sketchfab.com/3d-models/low-poly-cargo-ship-4c22cbaf01c1427f8ab60b3a07b1b32c) | Javier_Fernandez · CC-BY-4.0 | already-present-donor-restyled-this-change | Welded QEM at 1100/650/300 triangles; shared five-color palette, flat normals, no UV/texture payload, one LOD origin/height. |

## Source continuation status

[Поточні source зміни і перевірки](../NATIVE-FINISH-2026-10-08-UA.md): forest 1–10, adjacent yards, broken conductors і dam surfaces; нових native captures немає.

## Оптимізація й перевірка

Нові власні моделі: flat normals, shared palette, без текстур/rig/анімаційних кліпів, 6–932 трикутники на меш. Деталі, невидимі з висоти, не збільшувати штучно. Дві LOD-версії рослинності та дамби; fallen pylon не використовується як жива опора мережі.
Корабель: 2 384 → 1 100 трикутників, coarse 1 366 → 650. Silhouette 181 → 300 навмисно зберігає корпус і щогли; загалом усі ship LOD 3 931 → 2 050. Welded QEM не ріже сітку по кольорах; palette переноситься з найближчих donor faces. Спільні pivot/height запобігають стрибкам масштабу при LOD. UV/textures прибрано, оригінальний donor та CC BY атрибуцію збережено. Це вимір геометрії й payload, не обіцянка FPS.

[environment-kit-contact-sheet.png](environment-kit-contact-sheet.png) та [ship-lod-contact-sheet.png](ship-lod-contact-sheet.png) — raster inspection справжніх трикутників, не Unity render. Жодного generated concept image не використано як доказ готового меша.

У старих незмінених runtime streams виявлено 14 вироджених faces: flower_yellowA (1), sign (4), flower_yellowB (8), flower_purpleA (1). Вони зафіксовані як попередній стан, а не приховано виправлені через hand-edit generated Resources. Нові/перероблені меші перевіряються строго без вироджених faces.

Відтворення й перевірки описані в [tools/roadmap-assets/README.md](../../../tools/roadmap-assets/README.md). Native Bake Main і Bake Staging, сезонні ракурси та mobile metrics проводити лише після безпечного дозволеного запуску Unity. Літак/дамба/судно — окремі staging journeys; вони не додають сюжети в перші 10 main-рівнів.

## Усі roadmap-моделі

| ID | Трикутники | Каталог / статус |
|---|---:|---|
| `branch_greenhouse` | 228 | roadmap_story_models.json · runtime-available |
| `branch_lighthouse` | 60 | roadmap_story_models.json · runtime-available |
| `branch_pond` | 14 | roadmap_story_models.json · runtime-available |
| `branch_station` | 192 | roadmap_story_models.json · runtime-available |
| `campfire_stones` | 264 | roadmap_models.json · runtime-available |
| `flower_purpleA` | 76 | roadmap_models.json · runtime-available |
| `flower_yellowA` | 76 | roadmap_models.json · runtime-available |
| `flower_yellowB` | 154 | roadmap_models.json · runtime-available |
| `grass` | 132 | roadmap_models.json · runtime-available |
| `grass_leafsLarge` | 144 | roadmap_models.json · runtime-available |
| `log` | 200 | roadmap_models.json · runtime-available |
| `log_stack` | 184 | roadmap_models.json · runtime-available |
| `plant_bushSmall` | 16 | roadmap_models.json · runtime-available |
| `sign` | 44 | roadmap_models.json · runtime-available |
| `staging_aircraft_fore` | 5956 | models.json · staging-donor |
| `staging_aircraft_fore_coarse` | 1176 | models.json · staging-donor |
| `staging_aircraft_fore_silhouette` | 160 | models.json · staging-donor |
| `staging_aircraft_tail` | 1144 | models.json · staging-donor |
| `staging_aircraft_tail_coarse` | 1144 | models.json · staging-donor |
| `staging_aircraft_tail_silhouette` | 152 | models.json · staging-donor |
| `staging_cargo_ship` | 1100 | models.json · staging-donor |
| `staging_cargo_ship_coarse` | 650 | models.json · staging-donor |
| `staging_cargo_ship_silhouette` | 300 | models.json · staging-donor |
| `stone_largeA` | 80 | roadmap_models.json · runtime-available |
| `story_foundation` | 36 | roadmap_story_models.json · runtime-available |
| `story_memorial_garden` | 108 | roadmap_story_models.json · runtime-available |
| `story_trail_shelter` | 104 | roadmap_story_models.json · runtime-available |
| `story_workboat` | 120 | roadmap_story_models.json · runtime-available |
| `stump_round` | 56 | roadmap_models.json · runtime-available |
| `tent_detailedOpen` | 232 | roadmap_models.json · runtime-available |
| `tent_smallOpen` | 224 | roadmap_models.json · runtime-available |
| `tree_default` | 114 | roadmap_models.json · runtime-available |
| `tree_pineRoundA` | 204 | roadmap_models.json · runtime-available |
| `ua_abandoned_house` | 252 | models.json · staging-original |
| `ua_barn` | 216 | models.json · staging-original |
| `ua_beehive` | 96 | models.json · staging-original |
| `ua_bus_shelter` | 192 | roadmap_culture_models.json · runtime-available |
| `ua_bus_shelter_mosaic` | 197 | models.json · staging-original |
| `ua_chicken` | 43 | models.json · staging-original |
| `ua_chicken_coop` | 240 | models.json · staging-original |
| `ua_concrete_fence` | 300 | roadmap_culture_models.json · runtime-available |
| `ua_dam_breached` | 288 | models.json · staging-original |
| `ua_dam_breached_lod` | 144 | models.json · staging-original |
| `ua_field_weeds` | 12 | models.json · staging-original |
| `ua_field_weeds_lod` | 6 | models.json · staging-original |
| `ua_forester_hut` | 216 | models.json · staging-original |
| `ua_gate` | 312 | roadmap_culture_models.json · runtime-available |
| `ua_hydro_service_building` | 72 | models.json · staging-original |
| `ua_mooring_post` | 24 | models.json · staging-original |
| `ua_orchard_tree` | 132 | roadmap_culture_models.json · runtime-available |
| `ua_picket_fence` | 224 | roadmap_culture_models.json · runtime-available |
| `ua_picket_fence_damaged` | 120 | models.json · staging-original |
| `ua_plank_bridge` | 168 | models.json · staging-original |
| `ua_poplar` | 26 | models.json · staging-original |
| `ua_poplar_lod` | 22 | models.json · staging-original |
| `ua_power_pylon` | 932 | roadmap_culture_models.json · runtime-available |
| `ua_power_pylon_fallen` | 932 | models.json · staging-original |
| `ua_power_pylon_rusted` | 932 | models.json · staging-original |
| `ua_reed_clump` | 12 | models.json · staging-original |
| `ua_reed_clump_lod` | 6 | models.json · staging-original |
| `ua_rural_pole` | 120 | roadmap_culture_models.json · runtime-available |
| `ua_sunflower_patch` | 405 | roadmap_culture_models.json · runtime-available |
| `ua_sunflower_patch_lod` | 116 | roadmap_culture_models.json · runtime-available |
| `ua_wattle_fence` | 620 | roadmap_culture_models.json · runtime-available |
| `ua_well` | 140 | roadmap_culture_models.json · runtime-available |
| `ua_well_sweep` | 108 | models.json · staging-original |
| `ua_wheat_patch` | 480 | roadmap_culture_models.json · runtime-available |
| `ua_wheat_patch_lod` | 144 | roadmap_culture_models.json · runtime-available |
| `ua_whitewashed_house` | 604 | roadmap_culture_models.json · runtime-available |
| `ua_young_willow` | 54 | models.json · staging-original |
| `ua_young_willow_lod` | 22 | models.json · staging-original |
