[← Головна](../README.md) · [Довідник](README.md) · [Екрани](screens.md) · [Галерея](gallery.md) · [Розробка](development.md) · [Стан](status.md)

# Світ, сезони й подорожі

Чотири сезони змінюють колір, рослинність, опади й освітлення. Оточення розповідає про місце, а puzzle rules залишаються частиною level data. У campsite gameplay стоять намети гостей; нова мапа веде через зарослу українську долину між п’ятьма місцями.

| Весна | Літо |
| --- | --- |
| <img src="images/captures/2026-10-08/album-season-spring.png" width="460" alt="Весняний табір" /> | <img src="images/captures/2026-10-08/album-season-summer.png" width="460" alt="Літній берег і табір" /> |

| Осінь | Зима |
| --- | --- |
| <img src="images/captures/2026-10-08/album-season-autumn.png" width="460" alt="Осінній табір" /> | <img src="images/captures/2026-10-08/album-season-winter.png" width="460" alt="Зимовий табір" /> |

## Головна дорога

Committed [campaign.json](../QuietCamp/Assets/QuietCamp/Resources/QuietCamp/campaign.json) задає **110 ordered main places**, 15 districts і пʼять актів. `MvpLevelIds()` складає десять початкових authored IDs та сто generated IDs. Це стан content catalog, не твердження, що всі місця пройшли native visual/device acceptance. Старий README й baseline gallery описували перші 30; новий довідник відділяє цей попередній зріз від поточного каталогу.

<a href="../Design/Roadmap/CinematicPilot/2026-10-10/index.html"><img src="../Design/Roadmap/CinematicPilot/2026-10-10/gameview-composition-04.png" width="350" alt="Безперервна українська долина: Game View 10.10.2026" /></a>

Кадр показує новий пілот, перевірений 10.10.2026. [Історичний кадр старої мапи від 08.10.2026](images/captures/2026-10-08/04-roadmap.png) збережено; її реалізацію вилучено. Чинний пілот дозволяє QC001–QC005; решта кампанії та права доступу зберігаються в даних. Новий світ розповідає історію через мозаїчну зупинку, садибу, залишки ЛЕП, переправу й пошкоджену греблю. На мапі немає наметів чи сюжетних панелей. [Повний план кампанії](../CAMPAIGN_ROADMAP.md) містить більше запланованого контенту, ніж initial verified puzzle slice.

## Додаткові подорожі

Наведені нижче дані читаються з [monetization.json](../QuietCamp/Assets/QuietCamp/Resources/QuietCamp/monetization.json). `published` означає local catalog availability; це не факт публікації гри в магазині.

| Напрямок | Місць у каталозі | Прогрес для відкриття | Жаринки | Local catalog |
| --- | ---: | ---: | ---: | --- |
| Memories | 3 | 15 main places | 0 | published |
| Garden | 28 | 30 | 300 | published |
| Station | 18 | 45 | 300 | published |
| Lighthouse | 20 | 55 | 200 | **unpublished staging** |
| Mountain | 24 | 65 | 500 | published |
| Resort | 30 | 80 | 500 | published |
| Coast | 20 | 90 | 400 | published |

Умови доступу та purchase/provider behavior мають власні checks. Наявність JSON або `published` flag не підтверджує розвʼязність усіх нових puzzles, visual acceptance чи реальні store purchases. [Стан перевірок](status.md).

## Нова semantic roadmap composition

Новий [авторинг у main](https://github.com/kruty1918dev-ai/quiet-camp/tree/main/QuietCamp/Assets/QuietCamp/Authoring/Roadmap/Composition) містить forest/water sources, owned village parcels, damaged power routes і aircraft/ship/dam staging ensembles. Його основа — composition checkpoint і попередній 30-main handoff; 10.10.2026 усі гілки об’єднано в main. Новий п’ятиточковий світ використовує окремий native index і перспективну URP-камеру. [Свіжа галерея та статус приймання](../Design/Roadmap/CinematicPilot/2026-10-10/README.md). Історичні кадри довідника показують старий renderer; окремі [12 діорам стилю](https://github.com/kruty1918dev-ai/quiet-camp/tree/main/Design/Roadmap/DioramaStudies/2026-10-10) мають власні native Editor receipts.

[Звіт source-реалізації](https://github.com/kruty1918dev-ai/quiet-camp/blob/main/Design/Roadmap/NATIVE-FINISH-2026-10-08-UA.md) · [Матриця моделей](https://github.com/kruty1918dev-ai/quiet-camp/blob/main/Design/Roadmap/Assets/MODEL-MATRIX-UA.md) · [Composition tools](https://github.com/kruty1918dev-ai/quiet-camp/tree/main/tools/scene-composition).

Airplane-field і dam залишаються незалежними staging visual journeys; це не puzzles/unlocks перших main levels. Старі concept references або software mesh previews не є їхніми новими Game View renders.

Далі: [Атлас мапи й journey UI](screens.md#мапа-галявин), [Seasonal rendering](../Design/Atmosphere/SEASONAL-RENDERING-UA.md), [Story canon](../Design/Story/COZY-LORE-UA.md).
