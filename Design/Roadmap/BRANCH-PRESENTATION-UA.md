# Гілки як місця на мапі

Карта спочатку показує місце, стежку та landmark. Картка після вибору містить atmospheric preview, назву, короткий опис і completed/total. Умови доступу з’являються лише після окремого натискання «Роздивитися стежку». Вхід завжди повторно перевіряється через `GameServices.CanStart`/`JourneyAccessService` або bonus gate. Відкриття preview не змінює progress, кошти або entitlement.

## Authoring

`RoadmapBranchData` використовує stable id, anchorNodeId, target journey/bonus, type, visualIdentity, heroLandmark, descriptionKey, biome/season/lighting, world.props, requires, visibility/access rules, teaserDepth, nodes та accessOptions.

| Тип | Повна довжина | Main-map presentation |
|---|---|---|
| bonus | 1 node | Власне місце й один вузол |
| mini-trail | 3–5 nodes | Перший вузол, не більше одного silhouette далі |
| story-journey | 8–32 nodes | Відгалуження, hero landmark, два короткі segments максимум |

32 — поточний authoring safety budget однієї гілки, не ліміт main world. Для довших journey можна створювати наступний region у її власному каталозі. Для сумісності validator дозволяє legacy placeholder з порожнім nodes. Новий контент слід авторити з реальними level IDs, а не синтетичними preview IDs; цей етап залишає placeholders неопублікованими.

```json
{
  "id": "branch:garden",
  "anchorNodeId": "main:node:12",
  "journeyId": "garden",
  "type": "mini-trail",
  "visualIdentity": "garden",
  "heroLandmark": "branch_greenhouse",
  "titleKey": "branch.garden.title",
  "descriptionKey": "branch.garden.description",
  "biome": "meadow", "season": "summer", "lighting": "day",
  "teaserDepth": 1,
  "requires": ["main:level:12"],
  "accessRule": "embers",
  "accessOptions": [{"method": "embers"}],
  "published": false
}
```

Приклад неповний: звичайний exporter/validator також вимагає layout, preview і nodes/world. `requires` roadmap має узгоджуватися з `JourneyDefinition.requiredLevelIds`: останнє поле є авторитетною перевіркою для прямого запуску/continue, а не лише map UI.

Новий journey потребує: journey definition з реальними level IDs, summaries, authored roadmap catalog у `Resources/QuietCamp/Roadmaps/<journeyId>`, localization та branch entry. Navigation читає ID з даних; додавання switch/case для journey не потрібне. Перед published=true весь контент має пройти звичайний validator/solver pipeline.

## Презентація й reveal

Native renderer використовує ті самі baked low-poly meshes, шейдер, shadow pipeline, один матеріал і одну камеру. Від main path до landmark проходить фізична стежка; далі будуються лише дозволені teaser nodes. Невідомий другий node отримує generic silhouettes без authored story props. У projected presentation використовуються вісім pooled branch mesh slots з yielded preparation поза Canvas rebuild. Картка має один власний cached mesh і не додає preview camera/RenderTexture.

Firefly Glade — затемнена рослинність, ставок, до 16 світлячків на видимий viewport у спільному weather graphic. Lighthouse — світла вежа, береговий відтінок і каміння. Station — platform/rails/poles. Garden — теплиця, рослини та водний канал. Scenery формується offline exporter; для нового вигляду можна описати інший hero mesh і world.props, не змінюючи навігацію.

Reveal залишається похідним від ProgressionService: повний branch length не розкривається після unlock або покупки. Картка блокує map node interactions, зберігаючи світ видимим. Закриття повертає scroll anchor; orientation змінює layout й preview mesh, не створює другу активну world camera.

Story Trails показує тільки відомі гілки за тим самим reveal policy. Старий JourneyPreview залишається legacy fallback для сумісності; нові переходи з карти й Story Trails використовують compact branch card.

## Доступ і SDK boundary

Підтримувана vocabulary: progression, embers, rewarded, permanent-purchase, subscription, free-story.

- Progression/free story: published journey без paid entitlement плюс requiredLevelIds і послідовний journey progress.
- Embers: наявний `BuyJourney`, ціна береться з journey catalog. Двоетапне підтвердження, rollback при невдалому save збережено.
- Permanent purchase: `productId` + optional `IRoadmapBranchUnlockProvider`. Адаптер використовує receipt validation/персистентний journey grant; UI сама grant не видає.
- Rewarded: конкретний `placement` + optional adapter. Закриття відео або bool result картки не відкриває рівень; адаптер мусить підтвердити reward і зберегти grant.
- Subscription: `entitlementId` + active entitlement reader `GameServices.SubscriptionEntitlement`. JourneyAccess перевіряє його на кожному запуску; expired/revoked перестає давати доступ, permanent grant зберігає власний пріоритет.

Без adapter відповідні кнопки недоступні. Продукти й journeys цього етапу не опубліковані; catalog prices лишаються prototype. Не створено SDK, inventory, billing або backend. Local editable entitlement save сам по собі не є production security для real-money activation.

## Перевірки

`tools/export_roadmap_branches.py` створює shared models та presentation-only fixture чотирьох identities із 1/12/4/3 вузлами. Не змінює puzzles і не публікує optional content.

EditMode перевіряє type lengths, teaser bounds після progress і branch completion, metadata round-trip, моделі, access config diagnostics, prerequisites, free story та subscription revocation. PlayMode перевіряє чотири видимі місця, hidden future branches, bounded resources, actual Boot/Main navigation, card invitation → explicit access intent → close, tablet layout і повернення на мапу.

Результати й непідтверджені виміри: [QA-звіт](../../TestResults/roadmap-branches-2026-10-07/REPORT_UA.md).

## Main rollout — 2026-10-08

Основна кампанія використовує native `world3d` catalog, а не тільки Editor preview. Всі 30 рівнів і старі progress IDs збережені; opening seven-level композиція є частиною цього каталогу. В першому регіоні — неопублікована бонусна заготовка саду та teaser існуючої подорожі до маяка. Назва, опис і visual identity узгоджені: теплиця під «Зелений сад», маяк під «Стежка до маяка». Умови існуючих bonus/journey target не послаблено. Пристрої, SDK чи backend не підключалися.

При preview основний header і світ лишаються видимими; cream-картка містить місце, опис, progress та запрошення. Способи доступу відкриває окрема дія. My Camps доступні через повернення до Main Menu: новий roadmap header містить лише Back, назву й Story Trails.

Оновлена перевірка current catalog та переходів: [QA 2026-10-08](../../TestResults/roadmap-foundation-2026-10-08/REPORT_UA.md). Native capture чекає готового preview mesh та завершеного reveal; проміжні світлі silhouettes не видаються за фінальну композицію.
