# QuietCamp WorldOps

WorldOps редагує заморожені композиції через один C# Editor-сервіс. Майстерня та
`tools/qc_world.py` викликають той самий сервіс; Python не змінює рівні.

Рецепти: `QuietCamp/Assets/QuietCamp/Authoring/WorldOps/`. Робочий журнал, копії,
кадри та висновки: `UserSettings/WorldOps/<run-id>/` відкритого QA-проєкту.

```powershell
python tools/qc_world.py --project C:/Users/Public/qcui-20261006/QuietCamp run QuietCamp/Assets/QuietCamp/Authoring/WorldOps/pilot-audit.json
python tools/qc_world.py --project C:/Users/Public/qcui-20261006/QuietCamp status
python tools/qc_world.py --project C:/Users/Public/qcui-20261006/QuietCamp cancel
```

Початкова реалізація: синхронізація змін зі сталими ID, типізовані операції,
контроль ручних і зафіксованих об'єктів, перевірки геометрії, відновлюваний журнал
запису, CLI та секція майстерні. Нативні тести: 9 пройдено, 0 помилок
(2026-10-16, чинний Unity 6000.6.2f1).

Підключення Game View і діагностичних масок реалізоване, але ще потребує
наскрізного пілотного запуску. Наявні дефекти не вважаються прийнятими еталонами.
Запікання зміненого Terrain, кешування доказів і повне приймання 100 рівнів
залишаються наступними етапами. До їх перевірки цей документ не підтверджує
готовність усієї специфікації.

Звичайний запуск нічого не публікує без актуального художнього висновку.
`ReviewVerdict` має містити runId, levelId, round, inputHash, documentHash,
reviewer, accepted, inspectedFrameHashes, corrections та residuals.
Хеші повинні відповідати всім кадрам поточного ReviewPacket.
Команда `review <verdict.json>` передає висновок сервісу. Застарілі джерела,
кадри чи змінений пакет блокують застосування.

Маски `*.id-depth.png` та `*.isolated.png` — допоміжні зображення, а не ігрові
рендери. Червоний канал позначає головний об'єкт; зелений і синій кодують глибину
0–512 м. Справжні ігрові кадри з UI отримуються через ScreenCapture після
відтворення звичайного введення розміщення наметів.
