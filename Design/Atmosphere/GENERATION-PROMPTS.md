# Промпти генерації графіки

Дата: 30.09.2026. Засіб: вбудований image_gen. Lighting variants редагували morning.png, решта створена окремо. Бажані в промптах розміри не рівні фактичним: перевірені розміри в ASSETS-UA.md. Вихідні PNG збережені без творчої післяобробки.

## morning

Use case: stylized-concept
Asset type: actual production BACKDROP texture for a portrait low-poly camping puzzle game, not a concept screenshot. Portrait 3:4 aspect ratio, desired 1536x2048 PNG. No text, no UI, no tent, no campfire, no game board, no paths, no characters, no animals, no furniture, no foreground framing leaves.
Environment: a calm forest glade with stylized faceted pine trees and a few rounded polygonal broadleaf crowns. Camera looks into a forest clearing from a slightly elevated angle. Framing and depth are IMPORTANT: clean sky upper 0-27%; dark muted forest silhouettes occupy background horizon at y=25%-42% and far left/right edges only. The central region x=12%-88%, y=40%-96% is EMPTY gently graded low-detail sage-green meadow, completely clear of objects. An actual interactive 3D puzzle board will be rendered on top of this empty region in Unity, so do not draw any central rock, bush, tree, ground tile, ridge, island edge or landmark there. Distant trees remain darker than the open central meadow but not black, with soft atmospheric depth, gentle defocus ONLY on distant forest. Continuous natural surface, no literal rectangular blank panel. Polished cozy low-poly art matching QuietCamp red-tent forest style, restrained saturation, soft lighting and no noisy texture. All sides full bleed. Foliage silhouettes graceful and volumetric, not flat clipart.
TIME OF DAY: MORNING. Pale apricot horizon fading to light warm misty blue sky, tender golden early sun glow from upper right OUTSIDE the frame (no visible sun disc), soft mint/sage meadow, deep teal forest edges, extremely subtle mist confined to distant background. Bright readable calm center, dreamy but clean.

## noon

Edit this production low-poly forest glade backdrop into its noon lighting variant. EXACT same portrait canvas, forest silhouette geometry, camera, mountain and meadow layout as input, no new props. Keep the lower central meadow EMPTY for a 3D puzzle board to be drawn on top. Change lighting and colors only. No UI, text, tents, rocks, paths, characters or foreground leaves. NOON: clear pale blue upper sky, warm soft ivory horizon, softly sunlit sage green meadow, deep warm green and teal distant forest. Subdued summer midday, no harsh burnt yellow, no haze washing out trees.

## evening

Edit this production low-poly forest glade backdrop into its evening lighting variant. EXACT same portrait canvas, forest silhouette geometry, camera, mountain and meadow layout as input, no new props. Keep the lower central meadow EMPTY for a 3D puzzle board to be drawn on top. Change lighting and colors only. No UI, text, tents, rocks, paths, characters or foreground leaves. EVENING: peach/apricot horizon fading to dusty mauve upper sky, dusky muted sage meadow still light enough for a puzzle board, deeper blue teal tree silhouettes, faint warm rim lighting from upper right. Cozy sunset, no visible sun disc.

## night

Edit this production low-poly forest glade backdrop into its night lighting variant. EXACT same portrait canvas, forest silhouette geometry, camera, mountain and meadow layout as input, no new props. Keep the lower central meadow EMPTY for a 3D puzzle board to be drawn on top. Change lighting and colors only. No UI, text, tents, rocks, paths, characters or foreground leaves. NIGHT: deep desaturated indigo blue upper sky, faint tiny sparse stars ONLY in sky, muted teal horizon, dark blue green trees and moonlit sage teal meadow still readable, gentle silver blue illumination from upper right. No black crushed shadows. No visible moon, no lights or campfires.

## foreground

Create an actual game foreground overlay PNG with TRUE transparent alpha background, portrait aspect 3:4. No scenery, no text, no checkerboard pattern baked into pixels. Only dark blue-teal low-poly leafy shrub branches framing the bottom corners and slim lower side edges: left cluster fits x0-16%, y65-100%; right cluster fits x85-100%, y60-100%; bottom shallow strip y94-100%. Entire center x18-82% must be completely alpha transparent from top to bottom except bottom 6%. Top 60% entirely transparent. Several beautiful large faceted broad leaves, warm muted sage rims, dark undersides, tasteful soft defocus baked into edges for close-to-lens depth. Sparse curved twigs. Elegant cozy camping game, softly volumetric, simple large silhouettes. Fully transparent anywhere there is no leaf. Leaves dark but not black. No frame rectangle, no sky, no ground, no decorative border. This is a reusable separate overlay atop an existing forest background.

## leaves

Create an actual game sprite atlas PNG with TRUE transparent alpha background, square canvas, 2 by 2 equally sized cells with generous 15% padding inside EACH cell, no lines or labels. Exactly one isolated stylized low-poly leaf per cell. Top left a curved sage-green broad oval leaf with tiny stem, top right a narrower muted golden olive leaf, bottom left a small dark teal leafy twig with three leaflets, bottom right a warm muted amber round leaf. Cozy soft volumetric facets, subtle rim highlights, no photorealistic veins, natural organic curves. Consistent overhead light upper right. No cast shadow onto background. Leaf silhouette alpha clean with antialiasing, no halos. No text, no background color, no checkerboard drawn. Leaves will fly past camera and also gently drift in a low-poly camping game; legible at 16px. Do not join items between cells.

## rear

Create an actual separate distant forest layer for a cozy low-poly mobile camping game. Wide landscape aspect ratio 3:2 PNG with TRUE transparent alpha background. One continuous band of softly volumetric faceted pine trees and rounded broadleaf tree crowns across the BOTTOM THIRD of canvas. Tallest tree tip no higher than 48% canvas height. Forest base touches bottom edge. Middle trees shorter, taller trees at left and right. Dark desaturated teal #203e43 to muted blue green #35555b, subtle warm sage highlights on right side. Distant forest softly softened, simple graceful silhouettes without detailed branches. Entire sky/upper half completely alpha transparent. No sky, ground, mountain, text, glow, stars, tents or UI. This is a rear parallax treeline overlay, not a finished landscape. Fully transparent outside foliage silhouettes, no baked checkerboard.


