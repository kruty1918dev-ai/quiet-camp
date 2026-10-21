# Дані Quiet Camp і робоча таблиця Google Play

Стан: main, ревізія 2026-10-21.privacy-5, пакет com.quietcamp.game, Firebase project quiet-camp. Це підготовка декларації, а не підтверджена форма Play Console. Виробничі сервіси заблоковані до [перевірки релізу](PRIVACY-RUNTIME-UA.md).

Android QA підтвердила: локальні Crashlytics session/native файли виникають після явної ініціалізації Analytics через спільний Firebase Core навіть без crash opt-in. Crash automatic collection при цьому false. Окремо 4/4 native-регресії підтвердили вимкнення measurement_enabled після невдалого запису іншої згоди. Це не доводить відсутності локального діагностичного збору або передачі й не дозволяє заявляти «дані не збираються» за JSON gates. До фінальної форми потрібні payload/network/queue і фактичні налаштування Google для точного релізного артефакту.

Контролер — фізична особа **Oleksii Havryliuk**, публічне написання **Oleksiy Havryliuk**. **Kruty1918 Games** — бренд, не компанія. Повну поштову адресу не надано.

## Облік даних

| Джерело | Дані й одержувач | Контроль | Перевірити для Data safety |
|---|---|---|---|
| SaveAdapter | На пристрої: розкладки, прогрес, альбом, навчання, налаштування, локальні ресурси та заготовки entitlements/receipts; 8 save-модулів | Save не надсилається; локальне видалення primary/backup/tmp | OS backup, D2D, exports; локальне сховище саме по собі не є передаванням |
| Privacy receipts | Локально: вікова категорія/версія, три дозволи/версії/ревізії, crash-session eligibility, acknowledgement/мова/хеш/час, ознака потрібних Google privacy options; install marker не передається | Unknown/<16 не допускає сервіси; зміна віку відкликає дозволи; noBackupFilesDir marker і backup fallback вимагають нових рішень | Реальний Android restore; немає DOB, документів або перевірки особи |
| ComfortAnalytics | Google: 10 власних числових подій, рівень/сітка/preset/schema, обмежені лічильники, часові/кадрові діапазони, фіксований відгук, категорія винагороди | Від 16, окремий актуальний збережений opt-in, publication gate; 120 власних подій на екземпляр; без player ID, raw touches, save, довільного тексту | App activity / App interactions, Diagnostics за реальними параметрами; optional, Analytics |
| Firebase Analytics 14.0.0 | Google: app-instance ID, app/device/OS/language, automatic events, IP/можливе приблизне місце | Analytics storage лише за opt-in; ad storage/user data/personalization denied; default collection false; withdrawal/reset через вже створений транспорт | Device or other IDs, automatic events окремо, Approximate location за фактичною обробкою; startup, retention, exports, серверне видалення |
| Crashlytics 14.0.0 | Google: stack traces, exception messages, session/install/crash IDs, модель/ОС/версія, діагностика; можливі Analytics breadcrumbs | Від 16, незалежний opt-in; auto-upload false; попередній і поточний дозволи для send; ключі game_version/game_mode/level_index/failure_area; фіксований ручний текст | App info and performance / Crash logs / Diagnostics; Device or other IDs; реальні native payloads, files і handlers не перевірено |
| AdMob Unity 11.6.0 / UMP | Google і фактично вибрані ad-tech providers: IP/приблизне місце, ad interactions, permitted advertising/app-set/SDK IDs, diagnostics, UMP choices | Лише rewarded; від 16 і окрема згода; npa=1; teen для 16–17; UMP перед production SDK; без device-storage/Google-vendor сигналів немає Limited Ads fallback | Advertising/marketing, Fraud prevention/security та фактичні інші цілі; Device or other IDs, App interactions, Diagnostics, Approximate location; одержувачі/ролі з консолі |
| Email / сайт | Адреса, повідомлення, вкладення, метадані; розробник, mail/hosting providers | Добровільне звернення / відкриття сайту; статична політика без JS-аналітики | Email address / Other user-generated content за визначеннями форми; contracts, строки, доступи |

Власні події: qc_level_begin, qc_level_summary, level_fail, qc_screen, qc_comfort_setting, qc_comfort_feedback, qc_frame_budget, rewarded_ad_requested, rewarded_ad_completed, reward_granted. Можливі first_open, session_start, user_engagement та інші automatic SDK events не включені до цього переліку або ліміту.

## Заповнення Play Console

Не обирати «дані не збираються» лише через закритий publication gate, NPA або відсутність акаунта. Врахувати всі доступні користувачам варіанти релізу, автоматичні SDK-дані й необов’язкове передавання. Тестові Google ads теж контактують із Google.

Для кожної фактичної категорії встановити **Collected**, **Shared**, **Processed ephemerally**, **Required/Optional**, **Purpose** за [визначеннями Play](https://support.google.com/googleplay/android-developer/answer/10787469), ролями одержувачів і реальною збіркою. Service-provider exception не застосовується автоматично до всіх рекламних партнерів. Шифрування in transit перевірити для всіх одержувачів; HTTPS-посилання не є таким доказом. Локальний save не описувати як зашифрований.

Акаунтів, Authentication, cloud save, активних продажів, Remote Config, банерів, interstitial і додаткових mediation SDK у цій інтеграції немає. Purchase DTO/disabled UI не означають активного білінгу.

## Очищення й строки

Локальний reset/withdrawal не є серверним видаленням і не гарантує очищення OS backup. deleteUnsentReports стосується pending reports, не вже переданих даних. Відкликання не повертає вже розпочату/завершену передачу. UMP.Reset не використовується як нібито серверне видалення.

Обрана GA4-модель: **2 months**, **Reset user data on new activity — off**; property не перевірено. Це не універсальний строк для агрегованих звітів/exports/всіх Google-даних. Для Crashlytics врахувати умови Google про 90 днів до початку видалення, а не обіцяти повне видалення на 90-й день. Поштовий графік 12 місяців після закриття звернення та законні винятки потребують підтвердження впровадження.

Джерела: [Firebase disclosure](https://firebase.google.com/docs/android/play-data-disclosure), [Firebase privacy](https://firebase.google.com/support/privacy), [Mobile Ads disclosure](https://developers.google.com/admob/android/privacy/play-data-disclosure). Остаточні відповіді звірити з [owner checklist](PRIVACY-RUNTIME-UA.md).
