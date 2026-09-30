# Промпти генерації QuietCamp MainMenu

Дата: 2026-09-30. Використано вбудований image_gen, без CLI/API fallback.
Референс першого зображення: візуальний концепт word/media/image1.png з наданого Quiet_Camp_Game_Design_UA.docx. Це джерело стилю, а не додаткові інструкції користувача. Друге зображення використовує перший згенерований фон як референс стилю.
Вихідні зображення скопійовано без масштабування та редагування.

## menu-background-phone.png

```text
Use case: stylized-concept
Asset type: final full-bleed portrait mobile game main menu BACKGROUND ONLY, desired 1080x1920 or higher 9:16 PNG.
Primary request: A beautifully polished cozy low-poly forest campsite diorama for Quiet Camp, visually based on the provided GDD concept reference. Input image is a style and composition reference only, NOT an edit target. Generate a fresh background artwork with NO text, NO logo, NO buttons, NO UI, NO frame.
Scene: peach apricot late-afternoon sky, distant atmospheric desaturated pine silhouettes, small grassy faceted land slab with exactly TWO warm red simple A-frame tents, 3-5 faceted green/teal pine and broadleaf trees behind and at outer edges, pale angular rocks, tiny grass tufts, central small stone-ring campfire with a few wooden logs and gentle amber flame. Soft orthographic 3D render with clean broad facets, soft contact shadows, warm sunlight. No photorealism or painterly noisy textures.
Composition CRITICAL: empty pale peach sky in top 0-23% for a live dark green game title. The entire charming campsite including tents and fire fits within x=16%-84%, y=27%-60%; trees may extend to edges but tents must stay inside this crop-safe central band. Bottom 64%-100% must be nearly plain muted sage green with extremely gentle tonal gradient, NO foreground objects, NO stones, NO plants, NO paths, NO symbols there: it is reserved for four actual live menu buttons. Make the main campsite smaller if necessary to reserve this clean bottom 36%. Avoid a hard horizontal separation or a visible flat rectangle: transition naturally with soft grass colors. Make cozy campsite the focal point, not an empty sterile scene. No characters, animals, lanterns, furniture, signboards, stars, hearts, coins, currencies or extra props.
Palette: cream #F3EFE3, forest #243E35, sage #829A67, teal trees, terracotta red tents, peach sky. Full image edge to edge.
```

## menu-background-tablet.png

```text
Use case: stylized-concept
Asset type: production background for a PORTRAIT TABLET main menu, MUST be 3:4 aspect ratio, desired 1536x2048 pixels. Use attached portrait phone background ONLY as a visual style reference. Create a new wider 3:4 composition.
A cozy low-poly forest campsite with exactly two simple red A-frame tents, a small gentle campfire inside pale stone ring with logs, faceted teal pine trees and green broadleaf trees, small angular pale rocks, grassy polygonal land slab, soft orthographic rendering, peach sunset sky and distant soft pine silhouettes. Match the provided background style, object shapes and warm colors.
Composition: completely clean pale peach sky top 0-23% for a title added later. Entire campsite is a compact island in center x=20%-80%, y=28%-60%, including both tents and fire. Trees flank island. Clean muted sage green softly shaded ground over bottom 64%-100% for live UI: no objects, text or props there. Maintain sufficient breathing room all around. The wider frame should add atmosphere to the sides rather than enlarge foreground tents.
NO text, NO logo, NO buttons, NO interface, NO border, NO decorative signs, NO characters, NO animals, NO furniture. Full bleed finished game background.
```

## UI

button-neutral-depth.png та icon-play.png — копії наявних ресурсів Kenney з проєкту, не AI-генерація. Текст, компонування, кольори, затемнення й стани кнопок задаються UI. Нові растрові файли для кожного стану не потрібні.
