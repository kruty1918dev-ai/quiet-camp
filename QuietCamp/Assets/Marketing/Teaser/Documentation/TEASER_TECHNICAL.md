# Технічний процес

Editor-only збірка QuietCamp.Marketing.Editor ізольована від player assemblies.
TeaserConfig — явна конфігурація маркетингового монтажу (не нове джерело ігрових
правил). Master42 містить Cinemachine, Animation, Audio, Activation та Signal tracks. TeaserRig
містить сім редагованих камер. На записі Brain прив'язується до справжньої Camp camera.
Профіль Teaser окремий; сезон, світло, поверхні та звуки беруться з реального рівня.

WorldOps bridge делегує команду `teaser` розширенню, не створюючи зворотну залежність.
Кожен фрагмент стартує через LevelAuthoringPlayDriver у memory-only campaign preview.
Після готовності CampSceneHost повторюються законні команди CampSession.
Canvas приховується, звичайний input вимикається, а правила й події гри працюють.
Стани InputSystem devices зберігаються перед записом; наявні та щойно підключені
пристрої тимчасово блокуються, після зйомки початкові enabled-стани відновлюються.
Перед записом є prewarm; завантаження між рівнями не записується.

Recorder знімає кожен кадр Game View у 1920×1080, 30 FPS разом зі звуком AudioListener.
У цій Unity 6000.6 Recorder 5.1.7 зависав на WaitForEndOfFrame після Simulator.
`RecorderRenderBridge` викликає ту саму незмінену RecordingSession після реального
URP endContextRendering. Це адаптер внутрішнього API тільки для 5.1.7: інша версія
або відсутній метод зупиняє запис. Vendor-код та encoder не змінені.
Timeline камери обчислюються вручну за точним номером кадру; Brain застосовує їх
до чинної Camp camera. Повторний seek AudioClipPlayable заглушував музику, тому
під час offline capture звичайний AudioSource відтворює кліп із авторського Audio
track з відповідного sample offset. Музика й ігрові голоси записуються разом у Unity.
Сигнали `place-N` та `complete-camp` запускають справжні команди. Activation track
прибирає погоду перед завершенням. Окремий тимчасовий URP pass робить чорний cut
після всіх ефектів, щоб туман не забарвлював чорний екран. Жодного renderer asset не змінено.
Editor Mute Audio тимчасово вимикається й відновлюється. Експорт додає лише
master gain +6 dB до записаної доріжки, без зовнішніх звуків або музики; перевіряє
запас до clipping. `--gain-db 0` залишає початковий мікс гри.
Сім фрагментів складаються у MP4; перевірка декодування та ffprobe обов'язкова.
Збирання бере порядок і тривалість із завершеного job, не з другого hardcoded
списку. Candidate MP4 замінює фінальний файл лише після перевірки декодування.
Часовий розклад і seed фіксовані; DSP/частинки/Time шейдерів можуть трохи відрізнятися.
Робочі receipts містять хеш джерел, дії, кількість кадрів, рівень, memory-only та audio peak.
Зміна джерел під час дубля відхиляє його. Receipts не є художнім прийманням.

Журнал: UserSettings/Teaser/job.json; результати: Marketing/Exports/ у QA-проєкті.
Оригінальний Game View розмір, captureFramerate та якість відновлюються при завершенні.
Reload під час запису перериває дубль замість видавати його за успішний.
Вихід із Play Mode виконується наступним Editor update після короткого завершення
відновлення Game View, а не всередині coroutine гравця. Це усуває знайдений
NullReferenceException у GameView.OnPlayModeStateChanged поточної Unity.
Режим Preview не отримує recorded receipt; packager відхиляє його, перерваний
job, різні хеші джерел або невідповідність renderedFrames. Паралельні WorldOps
та legacy native mutations блокуються під час тизера.

Візуальне приймання зберігається окремим агентним verdict, прив'язаним до packet,
зображень і фінального MP4. Технічний export автоматично не означає художнє приймання.
