# Повторити тизер

Використовуйте один уже відкритий QuietCamp Editor, без player build.

1. За потреби синхронізуйте власні джерела: `python tools/install_teaser.py --project C:/Users/Public/qcui-20261006/QuietCamp`.
2. Unity: Assets > Refresh. Відкрийте звичайний Game View, не Simulator.
3. Tools > Marketing > Teaser > Create Assets виконується один раз і не перезаписує редаговану конфігурацію.
4. Open Teaser відкриває сцену. У TeaserConfig змінюйте параметри, у Master42 — анімації камер.
5. Validate Teaser перевіряє сім кадрів, 1260 frames, посилання й законні рішення.
6. Preview Teaser відтворює реальні сцени без запису; Record Teaser записує сім фрагментів.
7. Open Export Folder відкриває результати. Перед публікацією перегляньте монтаж і QA_REPORT.

CLI через ту саму точку WorldOps:

```text
python tools/qc_world.py --project C:/Users/Public/qcui-20261006/QuietCamp teaser create
python tools/qc_world.py --project C:/Users/Public/qcui-20261006/QuietCamp teaser validate
python tools/qc_world.py --project C:/Users/Public/qcui-20261006/QuietCamp teaser record --shot 2
python tools/qc_world.py --project C:/Users/Public/qcui-20261006/QuietCamp teaser record
python tools/qc_world.py --project C:/Users/Public/qcui-20261006/QuietCamp teaser status
```

`--shot` має індекси 0–6. Не запускайте паралельно WorldOps capture чи інший Recorder.
Постійний прогрес не очищається і не використовується.
