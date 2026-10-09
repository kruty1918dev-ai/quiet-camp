# 12 українських діорам — перший набір стилю

[Відкрити галерею](index.html) · [Увесь набір одним аркушем](overview.jpg) ·
[План дизайну мапи](../../DIORAMA-DESIGN-UA.md) ·
[Каталог моделей](../../ModelCatalogue/README-UA.md).

Native Unity Editor, 10.10.2026 за Europe/Berlin; UTC timestamps у
квитанціях можуть показувати вечір 09.10.2026. 12 фотографій 1200×900,
Unity 6000.6.2f1, OpenGLCore / NVIDIA GeForce 930MX. Це окремі art
сцени з редагованих композицій, не capture активної мапи чи нових рівнів.

Садиба, весняний сад, соняшниковий край, пасіка, березове узлісся,
соснова стоянка, вербовий ставок, очеретяний берег, лісова хатина,
осінній хутір, зимова садиба та сільська зупинка. Усі мають існуючі
намети Quiet Camp і відкритий табірний центр. Український власний набір
доповнено 16 переглянутими donor sources; в bindings ще є резервний
дерев'яний знак, який до зображень не увійшов.

`D*.json` поруч із фотографіями — квитанції generated result, mesh cost,
source/image hashes і GPU середовища. `visual-review.json` — окремі
явні художні спостереження для кожної діорами. Він не створюється
автоматично під час рендеру. Перший набір прийнятий як style studies,
production/mobile прийняття залишається окремим етапом.

Сильні відправні точки: D01, D04, D06, D07, D10. Для наступного проходу:
зблизити густе листя Synty з великими гранями власних дерев, додати
природні переходи ґрунту, освітлення та воду; перевірити читабельність
при справжньому розмірі roadmap картки. Невеликі деталі, зокрема панель
зупинки й грибний край, не є головним силуетом місця.

Native preview має окремі renderers для об'єктів і фотографічні контактні
плями, а не production batching/shadows. D07/D08 використовують 7
матеріалів; для майбутнього ліміту 6 потрібне об'єднання палітр. Renderers
дослідів перевищують майбутній ліміт 32 і мають бути зібрані в batches
перед runtime bake. Це не пройдені mobile budgets.

Збереження: `runtime-preservation.json` охоплює 2 084 чинні runtime/Main
файли; окремо повторно перевіряється baseline 562 файлів після передачі.
Не змінювались puzzles, кампанія, monetization, runtime code чи Main
authoring. Не запускався player build, install або publication мапи.

Відтворення з кореня репозиторію на цьому ноутбуці:

```bash
bash tools/render_diorama_editor_isolated.sh --probe
bash tools/render_diorama_editor_isolated.sh --studies
python3 tools/publish_diorama_gallery.py --sheets-only
# Переглянути кожне зображення й явно оновити visual-review.json.
python3 tools/publish_diorama_gallery.py
python3 tools/model_catalogue.py build
python3 tools/verify_diorama_studies.py
```

Renderer доступний також у `Quiet Camp/Dioramas/Render 12 Style Studies`.
На ноутбуці поряд із телефонною роботою Editor запускається тільки через
погоджену ізоляцію private PID/proc/network + masked USB; X11 socket для
графіки, один Editor і workers 1/4. Скінчені captures зберігаються в repo;
повна галерея окремих комерційних моделей залишається локально.
