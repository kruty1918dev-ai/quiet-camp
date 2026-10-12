# Композиція з автоматичним відбором — 12 жовтня 2026

[Методологія та схема систем](../METHODOLOGY-UA.md) описують повний шлях:
профіль → surface/layout → структурні об'єкти → ecological placement →
composition critic → краща допустима спроба → groundcover → host renderer.
Немає списку координат QC007 або правила «для цього level ID поставити дерево тут».

## Фактичні зміни

- `scene-placement`: critic із явними порогами та вагами, історія всіх спроб,
  видимість structural objects, deterministic ranking і стабільний tie-break.
- `vegetation-placement`: habitat fields у JSON, frozen species/shape inputs,
  читабельність через видимі ролі замість кількості об'єктів поза кадром.
- `rock-placement`: frozen geometry inputs і stage ownership для critic.
- QuietCamp: quality profile обов'язковий; дрібна зелень використовує effective
  seed обраної спроби. Native receipts містять review та всі attempt metrics.

## Результат відбору

| Рівень | Base seed | Обрана спроба (індекс від 0) | Score | Відхилені спроби |
|---|---:|---:|---:|---|
| QC007 | 12007 | 1 | 0.907 | Немає; обрано найвищий score серед 8 допустимих |
| QC001 | 41001 | 1 | 0.831 | 0 |
| QC009 | 41009 | 6 | 0.814 | 1, 3 |

Точні метрики та причини відмов — у JSON поряд із PNG. Для QC007 у
номінальному кадрі видно 11 decorative trees: 5 ліворуч і 6 праворуч;
Точні числа визначаються receipt, а не ручним підрахунком з картинки.
Score є геометричним proxy композиції, не відсотком схожості з AI-референсом.

## Нові native кадри

- [QC007 у справжньому Gameplay](QC007-reviewed-gameplay.png)
- [QC007 у широкому Gameplay](QC007-reviewed-gameplay-wide.png)
- [QC007 production-world preview](QC007-reviewed-landscape.png)
- [QC007 portrait](QC007-reviewed-portrait.png)
- [QC001](QC001-reviewed.png)
- [QC009](QC009-reviewed.png)

Це Unity world-camera PNG без зовнішньої обробки. Gameplay кадри приховують
HUD та service overlays; witness ставиться лише у renderer. Захоплення з
уже відкритого QA Editor, content hashes записані в receipts. Відмінність
історичного QA набору рівнів від canonical 100-level baseline лишається
такою, як описано у [попередньому етапі](../README-UA.md).

## Доказ переносності

[CompositionGallery](../../../../QuietCamp/Packages/com.kruty1918.scene-placement/Samples~/CompositionGallery/README-UA.md)
є окремою .NET 8 програмою, яка компілює ті самі пакети без Unity та QuietCamp.
Profiles `riverside-garden` і `cooling-courtyard` міняють assets/габарити/правила.
Верифікація перевіряє непорожній export і побайтовий replay обох профілів.
Обидва використовують поточну топологію прямокутного gameplay з бічним каналом;
це не native художнє приймання промислового біому або довільного жанру.

## Перевірка й критична оцінка

1098 .NET Core assertions, два portable host/replay checks і 20 Unity EditMode
tests пройшли. Canonical runtime/Editor/test sources та player-conditioned
sources скомпільовані. Gameplay flow і нові кадри перевірені у вже відкритому
Editor; після виходу QA saves відновлено. Player build не виконувався.

У native перегляді QC001 знайдено кроною закритий місток, хоча фізичного
зіткнення не було; додано structural visibility reservation і кадр повторено.
У console export знайдено порожню JSON serialization; замінено anonymous root
на typed artifact і додано перевірку вмісту, щоб byte replay не приймав `{}`.

AI-референс досі не відтворено піксельно. Critic не оцінює rendered materials,
тіні, animation artifacts або красу; ці критерії потребують native перегляду.
Зокрема форми крон і характер світла ще відрізняються від референсу. Нативна
мобільна продуктивність не виміряна.

Пакетні ревізії: scene-placement `0a4e999`, vegetation-placement `ceb109d`,
rock-placement `3371f44`. Вони відправлені в GitHub та вбудовані в обидві копії.
