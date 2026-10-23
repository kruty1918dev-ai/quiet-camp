# Приватність Quiet Camp

Поточна підготовка: 2026-10-22, гілка codex/production-readiness. Android package: com.quietcamp.game. Контролер: фізична особа Oleksii Havryliuk, публічно Oleksiy Havryliuk; Kruty1918 Games — бренд. Bundled policy залишається кандидатом; її публікацію не підтверджено.

Firebase Core / Analytics / Crashlytics 14.0.0, Google Mobile Ads Unity 11.6.0, Play Billing 8.0.0. Дозволи залежать від віку, мети та окремої перевірки; загального gate 16+ немає. Unknown не є дорослим. Child/LTD, персоналізація, Analytics, diagnostics і покупки мають незалежні правила. Локальна гра доступна без optional-згод; категорія не є перевіркою особи. Подробиці й неперевірені Android-сценарії — у поточному описі runtime.

Виробничі publication flags закриті. Нативна перевірка пристрою, Google-консолі, остаточний Data safety й підтвердження операційних строків зберігання ще потрібні. Поштова адреса й контакти підтверджені власником і відновлені з гілки редизайну. JSON і підміни SDK не підтверджують Android-реліз.

- [Реалізація, перевірки, блокери й owner checklist](PRIVACY-RUNTIME-UA.md).
- [Облік даних і таблиця Data safety](DATA-SAFETY-UA.md).
- [JSON власника: поля консолей, validate → prepare → apply → readiness](../../Config/README-UA.md).
- [Backend без розгортання: verification, RTDN, revocation, metrics](../../Server/README-UA.md).
- Вбудований текст: QuietCamp/Assets/QuietCamp/Resources/QuietCamp/privacy_content.json; legal.json задає ревізію й publication gate.
- Канонічна сторінка: https://oleksiy-havryliuk.de/quiet-camp/privacy, мови /uk, /de. Статичний текст у portfolio-oleksii-ha, legal/quiet-camp/content.json. Гілка підготовки не означає публікацію сайту.
- Синхронізація: python tools/sync_privacy_policy.py --site-root ../portfolio-oleksii-ha --write; перевірка без запису — без --write. Команда не відкриває production gates.

Старі templates/, site.json і hosting/operations інструкції — допоміжні заготовки, не актуальний опис установлених SDK або джерело політики. Фактичну поведінку визначають C# й Android source; перед релізом перевірити merged manifest і мережу.
