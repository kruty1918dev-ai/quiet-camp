# Візуальні пропозиції для рівнів Quiet Camp

Дата: 12.10.2026. Приклади: QC001, QC007, QC009. Вибір виконано через
`random.Random(20261012).sample(...)` серед десяти QC-рівнів відкритої копії
`C:/Users/Public/qcui-20261006/QuietCamp`; решта її індексу має generated IDs.

## Реальні моделі

`selected-project-models.png` — новий програмний рендер точних OBJ-трикутників
та кольорів з їхніх `palette.mtl`. Це геометричний перегляд, а не native Unity
capture. Джерела лежать у `QuietCamp/Assets/QuietCamp/Authoring/Roadmap/Models/`:

- `UkrainianRural/ua_orchard_tree.obj` — дерево для весняного узлісся.
- `EnvironmentKit/ua_young_willow.obj` — верба для берега.
- `EnvironmentKit/ua_reed_clump.obj` — очерет біля води.
- `EnvironmentKit/ua_plank_bridge.obj` — низький дерев’яний місток.
- `UkrainianRural/ua_wattle_fence.obj` — плетений тин.
- `UkrainianRural/ua_sunflower_patch.obj` — група соняшників.

Відтворення рендеру та схем: `python tools/render_level_visual_concepts.py`.
Використано наявний depth-buffer renderer без зміни source OBJ, MTL чи JSON гри.

## Концепти

- `QC001-concept.png` — весняний табір на краю саду: дерева й короткі секції тину.
- `QC007-concept.png` — літній берег після дощу: верби, очерет, місток поза полем.
- `QC009-concept.png` — літня галявина: соняшники, тин і тепле денне світло.

Концепти створено вбудованим `image_gen` із рендерами моделей і схемами актуальної
відкритої копії. `QC*-layout.png` точно відображають її поля, saved witness та
перешкоди; генеративні ілюстрації є пропозиціями оформлення, не перевіркою правил.
Усі нові декоративні об’єкти задумано поза ігровим прямокутником.

Архівні native Unity-кадри з `Design/Campaign100/2026-10-11/Renders/` використано
як референси стилю. Вони належать версіям рівнів із репозиторію, а не новим
capture відкритої копії. Для QC007/QC009 запропонована тема відповідає літнім
налаштуванням відкритої копії; JSON репозиторію має весняний варіант.

Повні запити та ролі зображень: [prompts.md](prompts.md).
Ці матеріали не публікують нові рівні й не активують видалену стежку.
