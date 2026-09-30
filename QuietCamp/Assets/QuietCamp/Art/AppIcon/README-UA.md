# QuietCamp — іконка гри без написів

Створено 30.09.2026 вбудованим image_gen. Червоний намет, тепле вогнище, зелений ліс. Без літер і заздалегідь заокруглених кутів.

- `quietcamp-icon-master.png` — оригінал генерації, 1254 × 1254 PNG.
- `quietcamp-google-play-512.png` — технічний експорт 512 × 512, 32-bit RGBA PNG, повністю непрозорий, менше 1024 KB. Лише пропорційне зменшення оригіналу через System.Drawing, без перемальовування.

Вимоги Google Play перевірено за [офіційною специфікацією](https://developer.android.com/distribute/google-play/resources/icon-design-specifications): 512 × 512, квадрат, до 1024 KB; маску й зовнішню тінь додає магазин.

Оригінал можна використати як джерело звичайної launcher-іконки в Unity. Це зведена ілюстрація, не набір окремих foreground/background шарів Android Adaptive Icon. Для адаптивної іконки потрібні окрема підготовка шарів і перевірка обрізання масками: [офіційна документація Android](https://developer.android.com/develop/ui/compose/system/icon_design_adaptive). Намет і вогнище розташовані в центрі, але фактична композиція ширша за запитану в промпті safe zone; автоматично вважати її адаптивною не слід.

Файли Player Settings, існуючий AppIcon.png і код Unity не змінено. Попередній варіант із написом не входить до цього комплекту.

## Промпт

```text
Use case: logo-brand
Asset type: finished SQUARE mobile game APP ICON, no lettering, for Google Play and Android launcher. Desired 1024x1024 PNG, 1:1 aspect ratio.
Create a distinctive premium cozy low-poly camping puzzle game icon. This is an ICON, not a wordmark, not a menu screenshot.
A single big terracotta-red A-frame tent with a warmly glowing golden entrance is the unmistakable main silhouette in the center, seen from a gentle elevated three-quarter angle. A small sculptural amber campfire in front of the tent, with only three chunky pale stones and two logs. Two simple deep-teal faceted evergreen trees behind tent, framing it. Soft sage grassy base subtly blends into the background, no floating island pedestal. Warm light and broad clean faceted surfaces. The tent is dominant, secondary props minimal. Friendly polished 3D toy-like low-poly aesthetic matching QuietCamp menu art.
Composition: central compact emblem, recognizable at 48 pixels. Main tent, campfire, and essential tree silhouettes entirely within central 60% diameter region so circular launcher cropping preserves them. Deep muted forest-green/teal background fills the ENTIRE square edge-to-edge, subtly lighter directly behind the red tent for contrast. Broad simple forms, few details, generous clean padding around central cluster, smooth soft lighting.
STRICT: ZERO text, ZERO letters, ZERO words, ZERO numbers, ZERO symbols resembling typography. No frame, no white outline, no badge border, no pre-rounded corners, no vignette to transparent, no transparency, no external drop shadow. Fully opaque full square image. No phone mockup. No extra tents, people, animals, furniture, banners, sky scene or landscape panorama. Produce just one final clean app icon.
```
