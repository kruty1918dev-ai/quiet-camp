# World Reveal

Reveal є похідним від `ProgressionService.CompletedIds`. Він не залежить від `LastLevelId`, scroll, replay, показаного preview або поточної орієнтації. Окремий fog save не потрібний: стандартний progress save та його існуюча міграція відновлюють ту саму видимість після restart.

`RoadmapRevealState` обчислює:

- Completed/Current: детальний світ до frontier включно. Старі saves з пропусками зберігають максимальний досягнутий frontier; невідомі ID не відкривають каталог.
- Near future: максимум дві сусідні main nodes у вигляді легких silhouettes; `revealDistance` і region `nearFuture` можуть зменшити цей горизонт.
- Unknown: жодних detail scenes або touch controls. Наявний ландшафт продовжується у серпанку, а не закінчується чорним прямокутником.
- Branch: максимум одна непройдена гілка з найближчим відомим anchor; завершені гілки залишаються. При однаковій відстані вибір стабільний за author order. `branchTeaser: 0` вимикає pending teaser.

Main-map branch presentation показує максимум перший node та silhouette другого, незалежно від того, чи journey містить 8 або 32 вузли. Bonus показує тільки один. Unlock не показує решту journey. Її власний roadmap catalog відкриває completed/current/near future за тим самим progress service; branch і main використовують різні level IDs. Для коректного completed/partial state authoring `branch.nodes` має містити стабільні реальні level IDs.

## Межа scroll

`MaxScrollDistance(viewportHeight)` обчислюється від останнього known node з невеликим простором для атмосфери. Відкриті entrances гілок також враховуються, щоб кінцевий bonus не став недоступним; довжина прихованої гілки межу не збільшує. Clamp застосовується в `ScrollRect.onValueChanged` та повторно в LateUpdate, з припиненням інерції; пряме присвоєння normalized position не обходить межу. Це не просто відсутність кнопок: камера не може дістатися далекого fragment. Старий anchor на hidden node замінюється на current; допустимий scroll до вже пройденого зберігається.

Авторський каталог не обрізається й не перегенеровується. Completed world можна переглядати назад через current ±1 chunk window. Не створюється по GameObject на кожний рівень.

## Атмосфера та плавність

Native 3D shader отримує progress horizon у world coordinates. Distance haze, низький height-dependent mist, зниження saturation та contrast змішуються з сезонним fog color. Дерева й рельєф лишаються справжньою 3D геометрією. Нові detail meshes готуються порціями під старим горизонтом; fog retreat починається після готовності геометрії. Протягом двох секунд горизонт плавно рухається вперед, одночасно з появою нових touch позначок, наметів та їхніх тіней. Projected meshes отримують cached CanvasGroup alpha без повторної генерації геометрії; fade починається після готовності мешів. Вже пройдені сцени не перебудовуються через кожний новий completion.

При поверненні з gameplay `GameServices.RoadmapSeenFrontiers` пам’ятає попередню показану межу тільки в RAM, щоб дати той самий перехід. При restart відновлення прогресу показує актуальний світ одразу; replay старого рівня не запускає повторний reveal. Reduced motion зберігає коротший спокійний dissolve без camera travel.

Projected renderer також використовує progress clamp і дешеву градієнтну атмосферу; це сумісний шлях під час міграції. У звичайній кампанії presentation switch цього етапу не змінюється.

Невідкритий сусідній region може явно описати `revealRules.farLandmarkAssetId`, `farLandmarkHeight` (1–15) та `farLandmarkX` (0–1). Тільки цей opt-in hero asset може бути слабким дальнім silhouette; його scene/detail props не активуються. `storyProps` не використовуються автоматично для невідомого регіону: так випадково не розкриваються сюжетні чи технічні докази. Силует доступний лише коли потрапляє у видиме просторове вікно, а не як HUD іконка.

## Перевірки

`RoadmapRevealTests`: fresh, 1 completed, 10 completed, branch unlocked, branch partial + restart, migrated GEN save, replay, nearest pending/completed branches, доступний кінцевий bonus. Перевіряються також різні viewport heights.

`RoadmapRevealPlayModeTests`: projected geometry-ready fade, справжній Boot/Main → roadmap, scroll/inertia bypass attempts, in-place completion + monotonic reveal, ten completions, tablet, stale hidden anchor та remount. Saves — RAM-only. Native captures перевіряють збіг Screen і PNG resolutions; isolated Editor layout резервується та відновлюється. Player builds не потрібні.

`tools/roadmap-pipeline` перевіряє ті самі portable policy contracts та bounded 360-level catalog. Дані benchmark не є playable campaign.

На цьому Linux Editor тривалі комбіновані прогони іноді завершувалися native thread/FMOD crash. Фінальні rendering/reveal та navigation сценарії запускаються окремими Editor-процесами; збій зафіксовано в Editor, коренева причина ще не доведена; стабільність player runtime окремо не підтверджена.

Ізольовані графічні тести вимикають AudioService через Editor-only `QuietCampBootstrap.EditorDisableAudio`; player behavior не змінюється. Це відокремлює перевірку rendering/reveal від native audio/thread crashes, зафіксованих у попередніх Editor-прогонах. Результати й обмеження вимірювання наведені у [QA-звіті](../../TestResults/roadmap-reveal-2026-10-07/REPORT_UA.md).
