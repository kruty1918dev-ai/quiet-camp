# Локальне встановлення інструментів майстерні

Unity: **6000.6.2f1**, URP/VFX Graph **17.6.0**, Unity Splines **2.8.4**.
Платні пакети залишаються на комп’ютері власника; у Git зберігаються адаптери й відтворюваний імпортер.

З кореня репозиторію:

```powershell
python tools/install_level_authoring_assets.py --help
```

Передайте шляхи до власних архівів River Modeler та MicroVerse, папки Sirenix
у Moyva і обох Unity-проєктів відповідно до параметрів `--help`.
Спочатку додайте `--dry-run`, перевірте перелік конфліктів, потім виконайте без цього прапорця.
Імпортер зберігає GUID і `.meta`, не переписує сторонні змінені файли та не імпортує демонстраційні сцени.
PWB 4.12.0 встановлюється наявним `tools/install_pwb.py`.

Наданий ZIP із назвою **1.6.20** містить модулі MicroVerse **1.7.0**:
Core, Splines, Objects, Vegetation, Ambiance. Автоматичне розміщення рослин не активується.
У River Modeler 1.0.4 є підтримка Stylized Water 3, але інтеграція очікує новіший
MicroVerse API `trenchCurve`. Імпортер замінює лише цей відомий виклик на старий
`trench`; адаптер майстерні створює два SplinePath для дна й м’якого берега.

Пакети використовували старі GUID залежностей. Імпортер переводить їх на названі
збірки Unity Splines, Collections, Burst, Mathematics і VFX Graph. Unity 6.6 використовує
вбудований Mathematics та сумісний forwarding-пакет 1.4.0. URP не понижується.
Відсутній необов’язковий MicroSplat Proxy не підключається.

`--refresh-compatibility` повторює тільки ці вузькі виправлення сумісності.
Власні матеріали пакетів зберігаються: робоча вода використовує окрему копію матеріалу Stylized Water 3.

Після імпорту дочекайтеся компіляції та відкрийте **Quiet Camp → Майстерня рівнів**.
Кнопка **Допомога** відкриває [офлайн-інструкцію](index.html).

Джерела API: [Unity Splines](https://docs.unity3d.com/Packages/com.unity.splines@2.8/manual/index.html),
[Odin Editor Windows](https://odininspector.com/tutorials/custom-editor-windows/odin-editor-windows).
Документацію саме старого River Modeler слід читати в PDF із наданого пакета:
[сторінка автора](https://staggart.xyz/river-modeler-docs/?section=microverse) тепер відсилає до локального PDF.
