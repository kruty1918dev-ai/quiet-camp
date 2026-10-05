# Gameplay effects

Quiet Camp combines the existing Atmos ambient sky, fire, foliage and phase
particles with scene-owned gameplay feedback:

- Placement: a small ring of fading soil/fabric motes and the quiet
  `sfx.tent.settle` landing cue, in addition to `placement.commit`.
- Pickup: a few grass fragments at the footprint, with shared wind drift.
- Fire: wispy rising smoke and warm embers with separate glow intensity.
- Weather: tapered falling rain, expanding ground rings and sparse pollen.
- Gusts: short leaf/dust streams originating on the upwind forest rim.
- Completion: a bounded golden sparkle burst and a 1.6-second warm tint,
  with existing completion/chime sounds and gentle ambience ducking.
- Invalid placement: the existing warning/toast plus `rule.invalid` and
  tactile warning. Rotation, pickup, undo/redo and removal have tactile cues.
- Buttons, switches and slider release have short tactile feedback; switches
  and slider release also play a soft selection sound.

## Quality and comfort

| Tier | Post effects | Placement particles | Completion particles |
|---|---|---:|---:|
| Low | No post pass | 4 (pool 12) | 12 (pool 16) |
| Balanced | Grading + vignette + Neutral tonemapping + HDR bloom (2 iterations) | 8 (pool 32) | 32 (pool 48) |
| High | Grading + vignette + Neutral tonemapping + HDR bloom (4 iterations) | 12 (pool 32) | 56 (pool 80) |

Reduced Motion suppresses gameplay particle emissions and clears already
living particles. It suppresses the animated completion tint. Sound volume
continues to use the existing saved Master/Effects bus levels. Vibration has
its own saved Comfort setting and is independent of Reduced Motion.

Runtime post profiles never modify shared assets. `QC_EffectsShaderVariants`
is an unattached profile that keeps runtime-only bloom and tonemapping shader
variants available under URP's post-processing variant stripping. Balanced
and High use their HDR-enabled pipelines with quarter-resolution bloom,
threshold 1.05 and high-quality filtering disabled. No depth-of-field,
motion blur or camera shake is added.

`CozyParticle` uses premultiplied alpha and an owned mipmapped atlas with
transparent gutters. Balanced/High soften surface intersections using the
existing forest depth texture; Low disables that sample. The shader handles
both orthographic and perspective depth and is Always Included for Android.
Only ember/firefly pools have gentle HDR glow. No extra camera, point lights,
collision module or shader keyword variants are required. Rain and gust
manual emissions explicitly start particle simulation. The detailed pool
budgets and checks are in [the cozy systems guide](COZY-SYSTEMS-UA.md).

The warm-world update adds scene-owned local fog, occasional sunbeams,
continuous offscreen-to-offscreen bird flights, batched forest details and a
110-second sunshine/cloud/drizzle cycle. Rain uses a bounded 12/60/96-drop
pool and the `ambience.rain` catalog key; Reduced Motion clears weather
particles and suppresses bird flights. Sun direction and puzzle rules stay
fixed. Current results and screenshots are in the
[warm-world QA report](../TestResults/warm-world-2026-10-03/REPORT_UA.md).

## Haptics package

The standalone MIT UPM package is published at
https://github.com/kruty1918dev-ai/haptics as `com.kruty1918.haptics`.
Quiet Camp references a fixed Git commit from `Packages/manifest.json`;
it requires no sibling checkout. Package tests are enabled via `testables`.

Android API 26+ uses finite VibrationEffect impulses, native presets on API
29+ and VibratorManager on API 31+. The package merges VIBRATE permission
from its Android library. iOS uses native UIKit feedback generators.
Desktop, WebGL and Editor are no-ops. Pause/focus loss, disabling haptics and
disposal cancel Android vibration; cooldowns prevent repeated input spam.
No feedback waits for an animation to finish.

Android platform references:
[Haptic APIs](https://developer.android.com/develop/ui/views/haptics/haptics-apis),
[VibrationEffect](https://developer.android.com/reference/android/os/VibrationEffect).

## Regeneration and checks

`QuietCamp → Generate Feedback Sounds` regenerates `tent_settle.wav` and
upserts its catalog entry while preserving other sounds. `Generate Extra
Sounds` also invokes it. WAV is owned procedural PCM mono at 44.1 kHz.

Package EditMode tests verify priority/cooldown, preferences, suspension,
unsupported devices and idempotent disposal. `EffectsPlayModeTests` verifies
particle budgets, isolated randomness, reduced motion, resource cleanup,
quality tiers and the saved haptics preference. `AudioPlayModeTests` verifies
that the new catalog key resolves to a usable clip.


## Дощ на мешах і згасання багаття — 03.10.2026

Замість горизонтальних billboard кіл використовується один mesh `RainImpacts` із 3/12/24 короткими контактами. `RainSurface` робить обмежені read-only запити по видимих трикутниках, фіксує barycentric координати й нормалі; кільця слідують за тканиною і переміщенням наметів. Readable увімкнено лише для дев’яти малих моделей; кеш shared геометрії reference-counted. Reduced motion очищає ефект. Детальні краплі й контакти доповнюють легкий фоновий дощ.

`CampRainShelter` гасить мокре багаття, його світло, жаринки й звук. Зігріваюче світло наметів використовує emission та м’який floor glow без додаткових Light. Освітлення й ефект не змінюють logical cells, правила або undo/redo. Докладна поведінка та межі — у [плані](COZY-SYSTEMS-UA.md).

## Тіні й вітер дрібної рослинності — 03.10.2026

`QuietCamp/FoliageLit` деформує вершини від спільного `WindSim` і використовує ту саму функцію у ForwardLit та ShadowCaster. Для травинок і квітів `_Cull=0`: тонкі листки відкидають тінь обома сторонами. Стовбури й суцільні крони зберігають back-face culling. Трава на полі, декор, квіти головного меню та рослинність альбому отримують і відкидають реальні тіні основного сонця.

Процедурні папороті й польові квіти більше не використовують нерухомий Simple Lit із вимкненими тінями. Сім невеликих об’єднаних мешів містять UV1 `(root.x, root.z, base.y, height)` для кожної рослини; `_ClusterWind=1` закріплює її основу й задає окрему фазу просторового вітру. Стебла й пелюстки мають спільний рух. Гриби та камінці залишаються нерухомими й також відкидають тіні. Bounds розширені на максимальний рух.

Немає фізики травинок, нових камер чи джерел світла з тінями. Залишено один каскад та наявні shadow atlases 512/1024/2048 з дистанцією 28/32/35 для Low/Balanced/High. На Low проріджується декор, процедурний густий підлісок прихований і не обчислюється flutter; видима трава зберігає основний вітер і тіні. Reduced Motion плавно зводить силу вітру до нуля, зберігаючи тіні. Дуже віддалені тонкі травинки можуть не розрізнятися в shadow map — щільність і роздільність потребують вимірювання на пристроях.

`VegetationShadowPlayModeTests` перевіряє реальні GPU зображення тіні prefab трави й одностороннього листка на трьох профілях, зміну тіні від вітру та протилежний бік освітлення; окремо перевіряє живі рослини Camp/Menu і reduced motion. Поради щодо бюджету: [Unity URP shadow optimization](https://docs.unity.com/en-us/engine/6000.5/manual/lighting-overview/lighting/shadows-in-urp/shadows-optimization).
