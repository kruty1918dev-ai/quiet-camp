# Промпт 02 — карта звуків і затишна просторова акустика
Застосуй спільний контракт START-HERE-UA.md. Нижче розділені факти перевірки та бажана поведінка.

## Результат аналізу наявного
Сканування всіх доступних через `rg --files Assets` WAV/OGG/MP3 дало **23 файли**. Активний `AudioCatalog.asset`: 11 основних + 6 extra, усі SpatialBlend0, усі effects вимкнені. Шість резервних OGG Kenney не додані в каталог. PCM виміряно для всіх 17 WAV; OGG включено до прослуховування, але їхній PCM не декодувався. Вбудований у FBX звук/Library/PackageCache не входив у сканування.

[AUDIO-MEASUREMENTS.md](AUDIO-MEASUREMENTS.md) — числові результати; [audio-analysis.json](audio-analysis.json) — шляхи, SHA256, importer і serialized settings. [audio-listening.html](audio-listening.html) — оригінали для слухової перевірки. **Це аналіз файлів і коду; я не підтверджую якість на слух.** RMS усього кліпу з паузами не є LUFS чи оцінкою суб’єктивної гучності.

Важливі висновки:
- Вітер24с: stereo44.1kHz, peak−2.85dBFS, RMS−9.61. Вогонь30с: stereo44.1kHz, peak−13.55, RMS−40.77. Різниця RMS31.16dB; поточні Volume .15/.20 її не компенсують. Не підняти вогонь на31dB автоматично: перевірити його активні фрагменти, піки й прослухати в міксі; спершу прибрати домінування вітру.
- Цвіркуни8с mono: peak близько0dBFS, 35 samples |x|≥.999; різниця крайніх samples петлі0.220184. Це ризик кліку при loop, а не гарантія його суб’єктивної чутності.
- Twig0.3с mono: 21 sample |x|≥.999; початок/кінець сильно різняться. Він одноразовий, тому великий seam не означає зіпсовану петлю; перевірити різкий початковий click та пікове обмеження.
- Wind seam0.000183, fire0.010529. Навіть малий endpoint delta не доводить безшовність: також оцінити зміну спектра/енергії в переході.
- Owl2.8с mono: peak−13.48, RMS−28.19. Gust4с mono: peak−5.07, RMS−21.56. Rustle0.7с mono: peak−2.51, RMS−19.09. Chime1.8с mono: peak−1.92, RMS−16.31.
- Усі11 SHA256 із JSON збігаються з оригіналами. Генеровані6 в JSON відсутні, але є у runtime SO. GetKeys() у QuietCampAudioCatalog зараз повертає лише основні sounds; узгодити перелік із реально доступними keys.
- Runtime читає Resources/QuietCamp/AudioCatalog.asset через QuietCampAudioCatalog. Редагування одного audio_catalog.json не підключає 3D чи extras.
- CampSceneHost запускає bird незалежно від вечора; day зупиняє fire/crickets, evening запускає їх. OnDestroy прямо не зупиняє ambience за ключами. Bootstrap має постійний wind. Перевірити фактичні owners/leases і прибрати дублікати, не зупиняючи чужі UI-звуки через StopAll.
- У проєкті немає активної музики; це не дефект. Спочатку повноцінна природна тиша, без обов’язкової музичної петлі.

## Очищення до мікшування
Працюй із похідними файлами в окремій директорії, не змінюй ліцензійні оригінали без потреби. Для власних procedural clips можна виправити генератор і відтворювано перегенерувати саме ці clips після порівняння.
1. Crickets: прибрати перевантаження генератора ДО clamp, лишити headroom; зробити коректну seamless loop із перекриттям 80–160мс. Crossfade розрахувати як нову циклічну хвилю з правильним wrap і довжиною, не fade-in/out до тиші кожні8с. Перевірити10 повторів. Для корельованого матеріалу почати з linear crossfade; equal-power може підняти гучність.
2. Twig: зменшити peak, перевірити початкові1–3мс; не стерти характер удару надмірним fade. Не використовувати його як кожний UI-click.
3. Rustle/chime: перевірити хвіст, потрібен плавний вихід2–10мс лише якщо є обрив.
4. Fire/wind: слухове порівняння loop point та import-компресії; якщо потрібне overlap playback, дві голосові доріжки на петлю рахуються в бюджет. Перевага чистому offline-loop.
5. Master не перевантажується в найгіршому збігу completion+fire+gust+UI. Орієнтир пікового запасу міксу≥3dB, додатково перевірити true peak експортом/аналізатором якщо доступно. Дані цього пакета — sample peaks, не true peaks.

## Карта «дія → звук → місце → частота»
Гучність нижче: бажані **відносні стартові множники після clip trim**, а не готові source Volume для нинішніх файлів. Головний орієнтир — тихий комфортний мікс при низькій гучності телефона. Один жест має один основний sonic reply.

| Подія | Наявний key / джерело | Простір | Поведінка |
|---|---|---|---|
| Відкрити кнопку меню / підтвердити діалог | ui.click / Minimalist1 | 2D Ui | 1 раз; cooldown80мс; max2 voices; gain.35 |
| Назад / закрити | ui.back / Minimalist2 | 2D Ui | .30; не дублювати ui.click |
| Обрати намет/об’єкт | ui.select / Minimalist3 | 2D Ui | .28; тільки зміна selection, не кожний кадр hover |
| Поворот об’єкта | placement.rotate / Minimalist4 | 2D або легка панорама точки | .30; тільки підтверджений крок; cooldown70мс |
| Валідне розміщення | placement.commit / Wood Block1 | 3D у центрі поставленого об’єкта | .48; один event після commit; max2 |
| Undo | placement.undo / Wood Block2 | 2D Sfx | .35; один успішний undo; невдала дія без удару |
| Невалідна спроба | rule.invalid / Minimalist5 | 2D Ui | .22; throttle350мс; м’який, без alarm/buzzer |
| Рівень завершено | level.complete / Minimalist6 | 2D Ui | .45; один раз на completion, не кожне відкриття панелі |
| М’яке післязвуччя перемоги | sfx.chime / chime_soft | 2D Ui | .12 через180мс; опційно, якщо не конфліктує з Minimalist6 |
| Базовий подих лісу | ambience.wind / Wind Soft Loop | 2D Ambience stereo | Одна петля; низько в міксі; fade1.5–3с; не залежить від камери |
| Живе вогнище | ambience.fire / Fire Burning Loop5 | 3D біля FireVisual | Петля лише коли fireActive; fade.8–1.5с; max1 |
| Птах далеко в кроні | ambience.bird / Bird Chirp06 | 3D на одному tree anchor | Morning18–35с; noon30–60с; evening60–100с слабко; night off |
| Нічні цвіркуни | ambience.crickets / crickets_loop | 2D Ambience bed | evening fade-in .3 веса, night1; morning0; max1 |
| Сова | ambience.owl / owl_hoot | 3D поза полем | Night55–100с, evening90–150с; max1; перші15с сцени тиша |
| Порив | ambience.gust / wind_gust | 2D Ambience або плавна stereo панорама | Тільки на реальний gust envelope; мін.18с між; .20; max1 |
| Листя поруч / камера входить у кущ | sfx.rustle / leaf_rustle | Для рослини3D; для переходу2D | .15–.35, max2; transition володіє своїм handle |
| Торкнутися сухої гілки в декорі | sfx.twig / twig_snap | 3D відповідний anchor | Лише явна взаємодія, якщо вона існує; .10; cooldown2с |
| Перемикач налаштувань | резерв switch-a/b.ogg Kenney | 2D Ui | A/B за on/off після слухового добору; не вводити якщо ui.select вже підходить |
| Легке натискання другорядної дії | резерв tap-a/b, click-a/b.ogg | 2D Ui | Кандидати заміни, не додаткові шари поверх кожного кліку |

Не додавати вигадані дії до gameplay заради файлу twig. Якщо об’єкт не інтерактивний, достатньо рідкого rustle від вітру. Жодних випадкових кроків, голосів, дзвіночків біля гравця без видимої причини.

## 3D і AudioListener
Один активний AudioListener. Поточна camera стоїть на відстані20; стандартні minDistance1 для поля можуть зробити всі об’єкти тихими. Обери явно:
- рекомендовано стабільний listener proxy над центром поля, орієнтація узгоджена з camera right/forward; висота близько2 клітинок, коректна ортонормальна rotation;
- не додавай другий listener до proxy; перенеси/вимкни старий зі збереженням сценового lifecycle;
- cosmetic plunge камери не пересуває listener різко. Doppler0 для ambience/UI/декору.

Нормувати дистанції до розміру клітинки c: fire min2c/max12c, bird/owls min4c/max24c, rustle min1.5c/max8c. Це старт, перевірити на найменшому/найбільшому полі. Custom rolloff має плавно сходити до0 біля maxDistance; сам maxDistance у logarithmic mode не слід трактувати як жорсткий silence cutoff.

Позиційні короткі файли — mono після перевірки phase при downmix. Stereo bed вітру лишити stereo. Crickets mono не перетворюється на stereo, якщо просто подвоїти канал. Просторовість забезпечують окремі рідкі джерела й реальна панорама; не розмножуй8с запис по всіх кущах. 3D AudioSource без HRTF spatializer — це панорама/затухання, не гарантований бінауральний звук. Новий важкий spatializer у базовий план не входить.

**Особливість наявного API:** AudioService.PlayAt → PlayInternal примусово ставить spatialBlend=max(current,1), тобто1. Min/Max налаштовуються раніше, лише коли definition.SpatialBlend>0. Тому гібрид .3/.6 через цей метод не працюватиме як очікується. Встановити коректні3D definitions для positional sounds; для гібридного сценарію мінімально розширити канонічний AudioPlayOptions і перевірити тести. Не обходитись ручним GetConfiguredSource.Play без обліку handle/pool/bus. Не змінювати загальний package API так, щоб зламати інших споживачів.

## Відлуння і повітря
Ліс не має звучати як кам’яна кімната. UI, вітер, цвіркуни, близькі placement — dry. Low без echo/reverb. Balanced/High: для ОДНОГО далекого owl/bird опційно дуже слабке outdoor echo: delay90–130мс, decay.10–.18, wet.025–.05, dry1. Почати з вимкненого; ввімкнути лише після A/B у навушниках і mono speaker. Не використовувати Room preset із дефолту пакета. Не сумувати echo+reverb на кожній пташці.
Якщо spatial reflection не робить сцену кращою, лишити dry+distance filtering. Для далеких подій low-pass орієнтовно6–10kHz; близькі не глушити. Не робити raycast-оклюзію кожного аудіоджерела щокадру; за потреби контрольні anchors/рідкі перевірки2–4Hz. Відстань, м’який тембр і тиша дають глибину дешевше.

## Спільна доба, пориви й мікс
Вітер реагує на той самий WindSnapshot, що трава: low-rate gain smoothing150–300мс, жодного Play щокадру. Візуальний gust плавно наростає приблизно.8с, тримається.5–1с, згасає1.5–2с; clip wind_gust4с стартує біля початку з відповідною envelope. Один event планує також не більше1–2 rustle anchors; не кожне дерево грає окремо.

При добі crossfade ambient weights6–10с. Bird/off не обривати посеред chirp; заборонити наступний. Owl, який уже почався, завершується природно. У меню той самий state, але менше випадкових подій. На паузі: gameplay one-shots зупинити/не породжувати; ambience можна тихо лишити за чинною продуктовою логікою. При background audio suspend не продовжувати scheduler і не накопичувати чергу птахів.

Buses: Master→Ui/Sfx/Ambience/Music (music може бути порожнім). Користувацькі налаштування мають останнє слово: gain = userBus × clipTrim × eventGain × distance × environmentEnvelope. Автодак не підвищує mute. Completion коротко duck Ambience до.75 за100мс, hold500мс, release900мс; звичайний tap не гойдає весь ліс.

Максимум одночасних голосів як стартова політика: Low8, Balanced12, High16; резерв2 для Ui і2 для gameplay feedback. Wind1+fire1+crickets1, решта one-shots. Поточне max8 PER KEY не є глобальним бюджетом. Не красти критичний commit заради сови; ambient events за відсутності місця пропустити. Handle для кожної loop; повторний Apply(snapshot) ідемпотентний. Scene-owned loops відпускаються на виході, bootstrap wind — у свого власника. Пул скидає фільтри, spatialBlend, rolloff, parent, position і mixer group перед повторним використанням.

## Імпорт і перевірка
Short UI/one-shot — Decompress On Load, PCM/ADPCM відповідно до вимірів пам’яті/якості. Long ambience — Vorbis/streaming лише після оцінки Streaming CPU і loop seam;8с mono crickets часто дешевше декодувати один раз. Не вмикати streaming усім малим clips. Значення importer у JSON звіряти з реальними .meta; runtime SO і Unity importer мають різні обов’язки. [Unity AudioClip](https://docs.unity3d.com/6000.0/Documentation/Manual/class-AudioClip.html) пояснює компроміси load types. [Unity AudioSource](https://docs.unity3d.com/6000.0/Documentation/Manual/class-AudioSource.html) — spatial blend та параметри джерела.

Приймання:10 loop wraps у навушниках; quiet phone speaker; mono downmix; без звуку теж зрозумілі помилки/успіх; night без денного bird spam;30 переходів без дубльованого fire/crickets; profile voices/CPU; mute/background/resume; відсутні key/clip fail без exception і лог-спаму. Порівняти peak, короткий RMS активних сегментів і слухову гучність, не нормалізувати природу під однакову «стіночку».

## Походження
KenneyUI/License.txt явно CC0. Наявні UI Soundpack, NaPH та Wind Soft Loop у manifest позначені як user supplied Moyva assets: їх не можна автоматично оголошувати CC0 чи перевикладати як власний звуковий пак. Generated6 мають локальний procedural generator QuietCampAudioExtras.cs. Цей комплект містить посилання на оригінали, а не копії ліцензійних записів. Нових завантажень не потрібно для базової реалізації. Подальші природні варіації bird/rustle — опційні; додавати тільки з підтвердженим походженням, ліцензією й слуховою перевіркою.
