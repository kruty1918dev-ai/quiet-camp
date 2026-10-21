# Приватність Quiet Camp

Поточна реалізація: main, ревізія 2026-10-21.privacy-5. Android package: com.quietcamp.game. Контролер: фізична особа Oleksii Havryliuk, публічно Oleksiy Havryliuk; Kruty1918 Games — бренд.

Установлено Firebase Core / Analytics / Crashlytics 14.0.0 та Google Mobile Ads Unity 11.6.0. Незалежні згоди початково вимкнені й доступні від 16 років. За невідомого віку та до 16 гра не запускає необов’язкові SDK-запити. Локальна гра доступна без згоди; вікова категорія не є перевіркою особи. Допустима rewarded-реклама має NPA, для 16–17 — teen treatment. Тестова реклама має ті самі вікові й локальні дозвільні обмеження.

Виробничі publication flags закриті. Нативна перевірка пристрою, Google-консолі, остаточний Data safety й підтвердження операційних строків зберігання ще потрібні. Поштова адреса й контакти підтверджені власником і відновлені з гілки редизайну. JSON і підміни SDK не підтверджують Android-реліз.

- [Реалізація, перевірки, блокери й owner checklist](PRIVACY-RUNTIME-UA.md).
- [Облік даних і таблиця Data safety](DATA-SAFETY-UA.md).
- Вбудований текст: QuietCamp/Assets/QuietCamp/Resources/QuietCamp/privacy_content.json; legal.json задає ревізію й publication gate.
- Канонічна сторінка: https://oleksiy-havryliuk.de/quiet-camp/privacy, мови /uk, /de. Статичний текст у portfolio-oleksii-ha, legal/quiet-camp/content.json. Гілка підготовки не означає публікацію сайту.
- Синхронізація: python tools/sync_privacy_policy.py --site-root ../portfolio-oleksii-ha --write; перевірка без запису — без --write. Команда не відкриває production gates.

Старі templates/, site.json і hosting/operations інструкції — допоміжні заготовки, не актуальний опис установлених SDK або джерело політики. Фактичну поведінку визначають C# й Android source; перед релізом перевірити merged manifest і мережу.
