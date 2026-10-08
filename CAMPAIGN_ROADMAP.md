# Quiet Camp — карта світу та структура кампанії (релізний масштаб)

Ціль: ~300 рівнів, що відчуваються як жива карта світу з центральною
сюжетною дорогою та відгалуженнями — а не лінійні 1–300.

## Загальна структура

- **Головна дорога (110 рівнів)** — `QC001–QC010` + `gen:qc_camp:1–32`
  + `gen:qc_camp:55–122`. П'ять актів; 15 районів по 3–8 рівнів.
- **Сюжетні гілки (143 рівні)** — journeys: маяк (staging), стара
  станція, теплиця, гірський маршрут, покинутий курорт, узбережжя +
  безкоштовні memories. Відкриваються прогресією / жаринками /
  покупкою; ніколи не перебудовують основну стежку.
- **Bonus/special (21 слот)** — приховані галявини, сезонні місця,
  challenge- і premium-слоти вздовж усієї дороги (кожні 5 рівнів).

## Головна дорога: акти й райони

| Акт | Рівні | Райони | Арка |
|-----|-------|--------|------|
| 1 | 1–30 | glades, forest, embers | Навчаємося створювати безпечні місця |
| 2 | 31–58 | bridges, stations, shores, farms, mills | Місця починають з'єднуватися |
| 3 | 59–82 | villages, passes, resort | Люди повертаються; гори й руїни курорту |
| 4 | 83–106 | coast, highlands, frontier | Витримані місця: берег, високогір'я, кордон |
| 5 | 107–110 | haven | Мережа місць завершена — світ живий |

## Ритм

- **Кожен 5-й рівень** — story beat: після-рівнева картка історії
  (`journey.main.story.N`, у `MonetizationCatalog` — біти призначаються
  на кожен 5-й рівень + на кінець кожного району; 31–42 лишаються
  щільним блоком переходу).
- **Кожні 10–15 рівнів** — новий район: одноразове інтро Остапа
  (`district.*.intro`), нові мотиви оточення від генератора.
- **Кожні 25–35 рівнів** — межа акту = завершення арки (останній район
  акту + story beat на межі).

## Сюжетні гілки

| Гілка | Рівнів | Відмикач |
|-------|--------|----------|
| memories | 3 (`gen:qc_camp:33–35`) | 15 місць головної дороги |
| garden | 28 (`gen:qc_gd:1–28`) | 30 місць + 300 жаринок |
| station | 18 (`gen:qc_st:1–18`) | 45 місць + 300 жаринок |
| lighthouse | 20 (`QC_LH001–008` + `gen:qc_lh:9–20`) | staging: unpublished, 55 місць |
| mountain | 24 (`gen:qc_mt:1–24`) | 65 місць + 500 жаринок |
| resort | 30 (`gen:qc_rs:1–30`) | 80 місць + 500 жаринок |
| coast | 20 (`gen:qc_cs:1–20`) | 90 місць + 400 жаринок |

Гілки повертають тих самих персонажів у нових обставинах і показують
наслідки рішень головної дороги (story keys per-level у journeys).

## Bonus/special слоти

Слоти в `bonus_camps.json` на позиціях кожні ~5 рівнів: стандартні
галявини, сезонні вікна (`seasonId`), premium- і challenge-варіанти.
Гейтинг: `afterLevel` + `requiredCompletions` (останній десяток рівнів)
+ `requiresPremium` + сезон. Масштабується до ~100 слотів додаванням
записів — рівні випікаються `gen:qc_bn:*`.

## Дані й пайплайн

- `tools/campaign-authoring/Program.cs` — детермінований план
  (id, pacing-number) для всіх сімей; валідатор + witness + солвер.
- `tools/regenerate_campaign.py` — frozen JSON + хеші + ContentRevisions
  + базові LegacyLevels для нових id + .meta.
- `campaign.json`: `generatedLevelIds` = порядок головної дороги,
  `districts` = райони/акти, `contentVersion` = cozy-campaign-4.
- Зона зайнятих `gen:qc_camp` id: 1–32 (main 11–42), 33–35 (memories),
  51–54 (bonus), 55–122 (main 43–110). Сім'ї гілок: qc_gd, qc_st,
  qc_lh, qc_mt, qc_rs, qc_cs; бонуси: qc_bn (21 випечений: 17 слотів
  активні, 4 у резерві).

## Стан реалізації

- Випечено й заморожено 270 рівнів: 110 main + 143 journey + 21 bonus —
  усі пройшли валідатор, збережений witness і незалежний солвер.
- Опубліковані раніше id не змінилися байт-в-байт; нові рівні отримали
  salted seeds (`IdSalt`), LegacyLevels-базові архіви та .meta.
- Мапа: центральна дорога + bonus-вузли кожні 5 рівнів + branch-вузли
  (`map-branch-stop`), що відкривають JourneyPreview; райони з act-CSS.
- Гілки без замороженого контенту автоматично unpublished
  (`MonetizationCatalog.Catalog` + `LevelLoader.Exists`).
- Локалізація uk/en/de: 501 ключ у кожній (district, journey.*,
  map.bonus.*, story beats); паритет перевіряється пробником.

## Поза межами

- Платні гілки лишаються staging: покупки вимкнені, entitlement —
  метадані для майбутньої активації.
- Rewarded-ads як відмикач — зарезервовано (немає інфраструктури реклами).
- Свіжий контент за оновленнями: сезонні вікна дадуть нові слоти без
  зміни коду.

## Checkout portability — 2026-10-08

Bundled AgentVerify, Atmos, LevelGen and LevelKit with recorded upstream revisions and original metadata/licenses. The manifest no longer requires four sibling repositories. `tools/verify_portability.py` checks local package containment, identity, dependency locks, Git pins and case-insensitive tracked paths without Unity caches. A fresh Unity import on Windows/macOS/Linux remains unverified: Editor launches are blocked by the active phone-session arrangement. No new renders or player builds.

Verification: current-source compilation passed for the five restored package runtime assemblies (including the LevelKit bridge), the four game layers and Editor/PlayMode test sources, using cached Unity references. This is compilation, not execution of Unity tests. The independent .NET 10 probe was not run: this host has SDK 9.0.203.
