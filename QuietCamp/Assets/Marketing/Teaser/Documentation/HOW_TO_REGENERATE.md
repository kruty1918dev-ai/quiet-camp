# Повторити тизер

Використовуйте один уже відкритий QuietCamp Editor, без player build.

1. За потреби синхронізуйте власні джерела: `python tools/install_teaser.py --project C:/Users/Public/qcui-20261006/QuietCamp`.
2. Unity: Assets > Refresh. Відкрийте звичайний Game View, не Simulator.
3. Tools > Marketing > Teaser > Create Assets виконується один раз і не перезаписує редаговану конфігурацію.
4. Open Teaser відкриває сцену камер/монтажу; справжній світ завантажується під час Preview.
   У TeaserConfig змінюйте target, pitch, yaw та ortho, потім Apply Camera Framing.
   Apply Configuration також переносить порядок/тривалість кадрів і відносні моменти
   сигналів у Timeline та застосовує вибрану музику. Сума мусить бути 1260 frames.
   Власні криві можна редагувати безпосередньо в Master42 / Animations.
5. Validate Teaser перевіряє сім кадрів, 1260 frames, посилання й законні рішення.
6. Preview Teaser відтворює реальні сцени без запису; Record Teaser записує сім фрагментів.
7. Запис створює сім native MP4; команда нижче збирає їх у фінальні 42 секунди.
8. Open Export Folder відкриває результати. Перегляньте монтаж і QA_REPORT перед публікацією.

CLI через ту саму точку WorldOps:

```text
python tools/qc_world.py --project C:/Users/Public/qcui-20261006/QuietCamp teaser create
python tools/qc_world.py --project C:/Users/Public/qcui-20261006/QuietCamp teaser validate
python tools/qc_world.py --project C:/Users/Public/qcui-20261006/QuietCamp teaser record --shot 2
python tools/qc_world.py --project C:/Users/Public/qcui-20261006/QuietCamp teaser record
python tools/qc_world.py --project C:/Users/Public/qcui-20261006/QuietCamp teaser status
python tools/package_teaser.py --project C:/Users/Public/qcui-20261006/QuietCamp
python tools/review_teaser.py --project C:/Users/Public/qcui-20261006/QuietCamp --final
```

`--shot` має індекси 0–6. Не запускайте паралельно WorldOps capture чи інший Recorder.
Постійний прогрес не очищається і не використовується.

Після редагування камер/Timeline у QA: `python tools/install_teaser.py --project C:/Users/Public/qcui-20261006/QuietCamp --collect`.
Collect переносить Unity assets та GUID назад у репозиторій, зберігаючи код і документацію.
Звичайний install зберігає вже відредаговані QA-камери й налаштування.
CLI точкового кадрування: `... teaser reframe --shot 0 --target 7 1 0 --ortho 5.2`.

Потрібні Python, numpy, Pillow та FFmpeg/ffprobe. На цьому ноутбуці encoder уже
наявний у `UserSettings/QuietCampPromoTools/`; його binaries не додаються в Git.
Код відтворення стартує зі стану вибраного shot. Довільний scrub ігрових команд
назад не підтримується: для чистого початкового стану повторно запускайте `--shot`.
