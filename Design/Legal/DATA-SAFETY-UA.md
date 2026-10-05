# Робоча таблиця Data safety

Стан джерел 2026-10-04. Ця таблиця допомагає заповнити форму; вона не є готовою відповіддю для всіх майбутніх релізів. Враховуються фактичні SDK, native auto-init, усі активні країни й доступні версії. [Google Play](https://support.google.com/googleplay/android-developer/answer/10787469?hl=en).

| Джерело | Дані | Поточний стан | Перевірити перед декларацією |
|---|---|---|---|
| SaveAdapter | Проходження, розкладки, альбом, косметика, налаштування, вибір аналітики та редакція політики | На пристрої | OS backup/restore, зовнішні exports, доступ інших SDK; у грі видаляються основний файл і локальні recovery-копії |
| ComfortAnalytics, власні події | Числові результати, лічильники дій/помилок, часові/кадрові діапазони, налаштування, добровільний відгук | Провайдер відсутній, збір вимкнений | App activity / app interactions та performance-related categories за визначеннями форми; purpose analytics, optional consent |
| Firebase Analytics | Власні події + SDK instance ID й технічні метадані | SDK не встановлено | Exact version, app/device IDs, SDK automatic events, processing locations, recipients, defaults, retention/export/deletion |
| AdMob/UMP | Дані згоди, рекламних запитів і показів; SDK identifiers/metadata | SDK не встановлено, ads=false | SDK disclosures, device IDs, advertising settings, приблизне місце/IP якщо обробляється, diagnostics, personalization, consent/regional flags |
| Підтримка / запит прав | Пошта, текст добровільного звернення; вкладення лише за бажанням | Контакт/backend не задані | Оператор пошти/форми, retention, access controls, encrypted transport, request logging |
| Майбутній account/cloud/IAP | Профіль, account ID, хмарні saves, purchase metadata | Не реалізовано | Новий аудит, account deletion, billing, disclosures, backups і processor agreements |

Не позначати SDK-ідентифікатори як анонімні. Власна схема без імен не прибирає технічні дані провайдера. Firebase дає окремі відомості для декларації, які треба звірити з інтеграцією. [Firebase](https://firebase.google.com/docs/android/play-data-disclosure).

Перед заповненням кожної категорії визначити: collected чи only on-device, shared за визначенням Play (із застосовними винятками для service provider), purpose, optional/required, ephemeral, encryption in transit, deletion route. Не прирівнювати будь-яку передачу обробнику до «sharing» автоматично та не вважати її завжди винятком.

## Протокол конкретного релізу

- Release/version: **[FILL]**
- Audited SDK versions/dependency manifest: **[FILL]**
- Countries / age protection design: **[FILL]**
- Effective public privacy revision/date: **[FILL]**
- Network observation: before consent / grant / revoke / offline / restart: **[NOT TESTED ON DEVICE]**
- Merged Android manifest / permissions / backup evidence: **[NOT TESTED FOR THIS RELEASE]**
- Server retention / deletion / exports / backup aging: **[FILL OR INAPPLICABLE WITH REASON]**
- Actual support/deletion rehearsal: **[FILL]**
- Reviewer / date / next review trigger: **[FILL]**

Зараз заборонено player builds. Наявні Editor-тести доводять інертність власних фабрик і роботу UI, але не доводять поведінку майбутніх native SDK або відсутність мережевого збору релізного APK.

## Межі очищення й системні резервні копії

Пакет збережень використовує `saves/slot00.mvs`, `slot00.mvs.bak` та `slot00.mvs.tmp`. Нове очищення даних гри видаляє всі три; інші файли/слоти не чіпає. При файловій помилці користувач отримує повідомлення про неповне очищення. Нове чисте збереження не містить старого прогресу, косметики чи згоди.

У поточному Android privacy manifest **немає визначеної політики OS-backup**. Не обіцяти, що локальна дія видалить системні копії, перенесення між пристроями чи історичні backups. До подачі визначити backup/restore-поведінку; згоду після restore не трактувати автоматично як актуальну без перевірки редакції та провайдера. Для Android 12+ одного `allowBackup=false` може бути недостатньо для D2D на деяких пристроях: потрібна перевірка відповідних extraction rules і реального merged manifest. [Android Auto Backup](https://developer.android.com/identity/data/autobackup).

SDK-згода реклами й власна згода аналітики незалежні. Поточне очищення даних гри відкликає власну аналітичну згоду, але не викликає `UMP.Reset()` і не видає рекламний consent reset за серверне видалення. Privacy options мають керуватися реальним провайдером перед його активацією.
