# Об’єднання всіх гілок у main · 10.10.2026

За прямим дорученням користувача всі локальні та віддалені гілки
об’єднано в `main`. Після `git fetch --all --prune` перевірка
`git merge-base --is-ancestor` підтвердила, що кожна з 11 записаних
refs уже входить в історію commit `77fd7c79421012d2cd87c47a76e90949f7a04c2f`.

Тому `main` переведено вперед із `7db19ff64384fd1673ff39f34c0da3cb7e6d1d6a`
на `77fd7c79421012d2cd87c47a76e90949f7a04c2f` без конфліктів, squash або переписування
історії. До `main` додано 39 доступних commits, включно з попереднім
merge art/composition та performance роботи. Source tree при цьому
залишився точно тим самим: `83890b24ff8caf8bc0e0843cd9dc07d0bfa49046`.

## Збережені вершини гілок

Кожен із цих commits є предком `main`; видалення назви гілки зберігає
всі її commits та файли в спільній історії. Повні hashes залишені тут
для пошуку історичного стану навіть після видалення гілок.

| Гілка origin | Збережений commit |
| --- | --- |
| `art/roadmap-native-finish-2026-10-08` | [`eb7ee6d1348bbac61dc6bc764265c0c6e610b1ee`](https://github.com/kruty1918dev-ai/quiet-camp/commit/eb7ee6d1348bbac61dc6bc764265c0c6e610b1ee) |
| `art/transfer-integration-2026-10-09` | [`77fd7c79421012d2cd87c47a76e90949f7a04c2f`](https://github.com/kruty1918dev-ai/quiet-camp/commit/77fd7c79421012d2cd87c47a76e90949f7a04c2f) |
| `docs/github-guide-2026-10-08` | [`1d5defb4c0b7d218c96993b0f334e36fdd6ce339`](https://github.com/kruty1918dev-ai/quiet-camp/commit/1d5defb4c0b7d218c96993b0f334e36fdd6ce339) |
| `perf/optimize-transitions-2026-10-09` | [`ec7d24362466f54b009862413f28a49d085f256b`](https://github.com/kruty1918dev-ai/quiet-camp/commit/ec7d24362466f54b009862413f28a49d085f256b) |
| `perf/performance-map-2026-10-08` | [`20ff0166296d6cab20216b82ac5096a6b19e69fa`](https://github.com/kruty1918dev-ai/quiet-camp/commit/20ff0166296d6cab20216b82ac5096a6b19e69fa) |
| `roadmap/composition-checkpoint-2026-10-08` | [`bafdaa4f02871da939771c764971828737c94577`](https://github.com/kruty1918dev-ai/quiet-camp/commit/bafdaa4f02871da939771c764971828737c94577) |

Локальні `art/transfer-integration-2026-10-09`,
`perf/optimize-transitions-2026-10-09` і
`roadmap/composition-checkpoint-2026-10-08` збігалися з відповідними
віддаленими вершинами. Додаткових worktrees та відкритих PR на момент
перевірки не було.

## Перевірки

- Ancestry: усі локальні й віддалені вершини входять в `main`.
- Source tree: перенесення в `main` зберегло дерево `77fd7c7` без змін.
- `python3 tools/verify_diorama_studies.py`: 12 source-linked native
  діорам, 17 donor bindings, 2 084 файли чинної кампанії та 562 файли
  transfer baseline збережені за SHA-256.
- `python3 tools/verify_transfer_import.py`: 8 920 payload files,
  1 083 native adaptations і 7 213 унікальних Unity GUID перевірені.
- `python3 tools/verify_portability.py`: залежності й paths checkout.
- `python3 tools/verify_github_docs.py`: links, captures та переклади.
- `git diff --check`: форматування змін документації.

Після перенесення оновлено документацію про поточне розташування
авторингу, посилання на колишні art-гілки та receipt повторної перевірки
імпорту (кількість Unity GUID тепер враховує діорами).

Перший запуск перевірки довідника виявив розбіжність у CSS generator:
після попереднього merge canonical CSS містив правила підготовленої
branch-preview панелі, які генератор помилково включав у stylesheet
чинної scrolling map. `tools/prepare_ui_styles.py` тепер виключає ці
панельні класи. Повторний `--check` підтвердив точний збіг усіх шести
наявних stylesheet assets; жоден CSS asset гри не змінювався.

Авторинг, активна мапа, пазли, прогресія та збереження не змінювалися. Unity під час об’єднання
не запускався; нові рендери, виконання Editor/PlayMode tests, player
builds і device acceptance цим кроком не заявляються. Receipts
попередніх рендерів зберігають свої початкові дати й source hashes.

## Завершення очищення гілок

Після успішного push `main` віддалені назви шести гілок видалено одним
atomic push з окремим `--force-with-lease` для кожної перевіреної вершини.
Три локальні гілки видалено через `git branch -d` після перевірки ancestry.
Повторний fetch і live `git ls-remote --heads origin` підтвердили, що
локально та на GitHub лишилася тільки `main`; `origin/HEAD` указує на
`origin/main`. Усі 11 початкових refs збережені в ancestry `main`.
