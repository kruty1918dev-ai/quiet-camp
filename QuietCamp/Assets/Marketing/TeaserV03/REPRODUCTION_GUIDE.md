# Як повторити V03

Виконувати з кореня quiet-camp. Потрібен **один уже відкритий Editor**
QA-проєкту `C:/Users/Public/qcui-20261006/QuietCamp`; ці команди не запускають
новий Editor, build або ADB. PlayMode використовує `Save.MemoryOnly`.
Не записувати одночасно з ручним редагуванням гри.

## Середовище

Перевірено: Unity 6000.6.2f1, URP 17.6.0, Cinemachine 3.1.7,
Timeline 6.6.0, Recorder 5.1.7. Unity-пакети залишаються на ліцензіях
Unity; не замінювати їх випадковими новішими версіями.
Python 3.14.7, OpenCV headless 4.13.0.92, PySceneDetect 0.6.7.1.
QA venv: `UserSettings/TeaserV03Tools/venv`. Пакети не додаються глобально.
FFmpeg 9.0.2 essentials, локально `UserSettings/QuietCampPromoTools/`;
фактична збірка має `--enable-gpl --enable-version3`, отже GPLv3+,
а не загальне припущення LGPL. Використовується як окремий інструмент;
двійкові файли не додаються в Git і не вбудовуються в гру.

Джерела й ліцензії перевірено перед встановленням:
[OpenCV, Apache 2.0](https://github.com/opencv/opencv),
[PySceneDetect, BSD-3-Clause](https://github.com/Breakthrough/PySceneDetect),
[FFmpeg](https://github.com/FFmpeg/FFmpeg).
[Unity MCP, MIT](https://github.com/CoplayDev/unity-mcp) не встановлено:
WorldOps уже дає потрібний доступ. [OTIO, Apache 2.0](https://github.com/AcademySoftwareFoundation/OpenTimelineIO)
не встановлено: редаговані Timeline й EDL достатні. Ці рішення не є залежностями.

## Підготовка й три вступи

```powershell
$qa = 'C:/Users/Public/qcui-20261006/QuietCamp'
python tools/teaser_v03_score.py --output QuietCamp/Assets/Marketing/TeaserV03/Audio/quietcamp_v03_score.wav
python tools/install_teaser.py --project $qa
python tools/refresh_existing_editor.py --pid 7632
python tools/qc_world.py --project $qa teaser create --version v03-a
python tools/qc_world.py --project $qa teaser create --version v03-b
python tools/qc_world.py --project $qa teaser create --version v03-c
python tools/teaser_v03.py --project $qa hooks
```

PID є прикладом поточної сесії; перевірити, що це саме потрібний Editor.
Якщо assets уже є, `create` їх **не скидає**. Вікно, камери, профілі й Timeline
редаговані. Не видаляти assets для «оновлення», інакше втратяться авторські зміни.
Три прототипи необхідно переглянути, перш ніж обирати повний монтаж.

## Rough → перегляд → fine

```powershell
python tools/qc_world.py --project $qa teaser create --version v03-rough
python tools/teaser_v03.py --project $qa rough
# Перегляд rough та виправлення постановки ПЕРЕД наступним записом.
python tools/qc_world.py --project $qa teaser create --version v03
python tools/teaser_v03.py --project $qa fine
```

У Unity: `Tools > Marketing > Teaser > V03 > Open Review Timeline`.
Є `Preview Review` і `Record Review`; конфігурація має окремі assets усіх
планів. Після редагування камер/тривалості виконати `Apply Configuration`
або `qc_world.py ... teaser bake --version v03`. Нові сигнали виконують лише
дозволені команди; для гри залишається канонічний `CampSession`.
Для камер CLI використовує, наприклад,
`teaser reframe --version v03 --shot 0 --target -2 .6 -1.5 --ortho 2.35`;
повний список параметрів — `qc_world.py teaser --help`. Зміни проходять C#-сервіс.

## Перевірка запису

```powershell
$py = "$qa/UserSettings/TeaserV03Tools/venv/Scripts/python.exe"
& $py tools/test_teaser_v03.py
& $py tools/review_teaser_v03.py --project $qa --version v03-a
& $py tools/review_teaser_v03.py --project $qa --version v03-b
& $py tools/review_teaser_v03.py --project $qa --version v03-c
& $py tools/review_teaser_v03.py --project $qa --version v03-rough
& $py tools/review_teaser_v03.py --project $qa --version v03
python tools/install_teaser.py --project $qa --collect
```

Для нового QA venv встановити `tools/teaser_v03_requirements.txt`; він потребує
сумісного Python, FFmpeg і локальних Unity-пакетів проєкту.
`package_teaser.py` робить точні склейки та перевіряє повне декодування MP4.
`review_teaser_v03.py` створює контакти, аналіз склейок, повторів, чорного,
звуку й підписаний хешами пакет. Алгоритмічні прапорці оцінює агент.
Ці скрипти **не присвоюють художній статус Final**.

`fine` автоматично передає пакувальнику `--finish-v03`: компресор і лімітер
вирівнюють записаний у Unity мікс; останній fade завершується на кадрі 474.
Це фінальна обробка запису, без нових звуків чи намальованих ігрових подій.
Фактичні параметри збережено в `export-verification.json` і пакеті QA.
Після перегляду агент може виконати `teaser_v03_verdict.py --project <QA>
--decision Review --scores 3.5 3 3 3.5 3.5 --notes <QA_REPORT.md>
--unresolved-gates 1 2 3 4 6 7 9`. Команда перевіряє актуальність джерел,
MP4 і всіх переглянутих картинок; це оцінки агента, а не автоскоринг.
Після verdict виконайте `python tools/teaser_v03.py --project <QA> deliver`:
п’ять перевірених відео, дев’ять документів, QA й атрибуція збираються в ZIP
із SHA-256 manifest, перевіркою CRC та межею 100 MB. За наявності початкових
знімків команда також перевіряє V01/V02 та локальний прогрес.

Результати: `Marketing/Exports/v03/<version>/`. Кожний запис зберігає
`Shots/*/gameplay.mp4`, `capture.json`, Timeline, конфігурацію та журнал
`UserSettings/Teaser/job-<version>.json`. Перед перезаписом зберегти потрібний
попередній дубль. V01/V02 мають свої окремі каталоги й не перезаписуються.
Не редагувати вручну квитанції, sourceHash або згенеровані дані кампанії.
У разі помилки: `qc_world.py ... teaser status|cancel --version <version>`;
не складати відео з неповного журналу.

## Межі відтворюваності

Seed, камери, дії та кількість кадрів фіксовані. Реальні ParticleSystem,
DSP та погода можуть відрізнятися між машинами; побітову тотожність відео
не обіцяємо. Зберігається редагований задум та перевірений процес запису.
Фінальний MP4 має назву Review, доки всі художні категорії не досягли 4/5.
