# Технічний процес

Editor-only збірка QuietCamp.Marketing.Editor ізольована від player assemblies.
TeaserConfig — явна конфігурація маркетингового монтажу (не нове джерело ігрових
правил). Master42 містить Cinemachine, Animation та Audio tracks. TeaserRig
містить сім редагованих камер. На записі Brain прив'язується до справжньої Camp camera.
Профіль Teaser окремий; сезон, світло, поверхні та звуки беруться з реального рівня.

WorldOps bridge делегує команду `teaser` розширенню, не створюючи зворотну залежність.
Кожен фрагмент стартує через LevelAuthoringPlayDriver у memory-only campaign preview.
Після готовності CampSceneHost повторюються законні команди CampSession.
Canvas приховується, звичайний input вимикається, а правила й події гри працюють.
Перед записом є prewarm; завантаження між рівнями не записується.

Recorder знімає кожен кадр Game View у 1920×1080, 30 FPS разом зі звуком AudioListener.
Сім фрагментів складаються у MP4; перевірка декодування та ffprobe обов'язкова.
Часовий розклад і seed фіксовані; DSP/частинки/Time шейдерів можуть трохи відрізнятися.
Робочі receipts містять хеш джерел, дії, кількість кадрів, рівень, memory-only та audio peak.
Зміна джерел під час дубля відхиляє його. Receipts не є художнім прийманням.

Журнал: UserSettings/Teaser/job.json; результати: Marketing/Exports/ у QA-проєкті.
Оригінальний Game View розмір, captureFramerate та якість відновлюються при завершенні.
Reload під час запису перериває дубль замість видавати його за успішний.
