# Вимірювання звуків

WAV PCM scan, SHA256, serialized catalog and importer audit. No perceptual listening claim. Seam metric is endpoint difference, not proof of a seamless loop. DC column is the mean sample offset.

| Ключ | Файл | с | Канали | Гц | Peak dBFS | RMS dBFS | DC | Шов Δ |
|---|---|---:|---:|---:|---:|---:|---:|---:|
| ui.click | Minimalist1.wav | 0.76 | 2 | 44100 | -10.47 | -45.67 | -0.00001 | 0.000015 |
| ui.back | Minimalist2.wav | 0.76 | 2 | 44100 | -10.74 | -46.50 | -0.00002 | 0.000000 |
| ui.select | Minimalist3.wav | 0.75 | 2 | 44100 | -12.85 | -45.25 | -0.00002 | 0.000015 |
| placement.rotate | Minimalist4.wav | 0.75 | 2 | 44100 | -9.50 | -41.05 | -0.00002 | 0.000015 |
| placement.commit | Wood Block1.wav | 1.29 | 2 | 44100 | -8.38 | -35.13 | -0.00002 | 0.000015 |
| placement.undo | Wood Block2.wav | 1.29 | 2 | 44100 | -8.38 | -34.65 | -0.00002 | 0.000000 |
| rule.invalid | Minimalist5.wav | 0.75 | 2 | 44100 | -10.12 | -42.28 | -0.00002 | 0.000015 |
| level.complete | Minimalist6.wav | 0.75 | 2 | 44100 | -11.51 | -44.68 | -0.00002 | 0.000031 |
| ambience.wind | wind_loop.wav (generated) | 23.8 | 2 | 44100 | -9.10 | -25.36 | -0.00036 | 0.000702 |
| ambience.fire | Fire Burning Loop 5.wav | 30 | 2 | 44100 | -13.74 | -40.91 | +0.00000 | 0.007065 |
| ambience.bird | Bird Chirp 06.wav | 1.47 | 2 | 44100 | -10.34 | -40.12 | +0.00000 | 0.000000 |
| ambience.crickets | crickets_loop.wav | 7.88 | 1 | 44100 | -5.20 | -22.86 | -0.00000 | 0.004822 |
| ambience.owl | owl_hoot.wav | 2.8 | 1 | 44100 | -13.48 | -28.19 | +0.00000 | 0.000000 |
| ambience.gust | wind_gust.wav | 4 | 1 | 44100 | -5.07 | -21.56 | 0.00000 | 0.000000 |
| sfx.rustle | leaf_rustle.wav | 0.7 | 1 | 44100 | -2.51 | -19.09 | -0.00001 | 0.046753 |
| sfx.twig | twig_snap.wav | 0.3 | 1 | 44100 | -4.51 | -27.75 | +0.00002 | 0.000000 |
| sfx.chime | chime_soft.wav | 1.8 | 1 | 44100 | -1.92 | -16.31 | -0.00000 | 0.000092 |

Примітки:

- `ambience.wind` — процедурний стерео-шар (замінив ліцензований Moyva «Wind Soft Loop», що вимірювався як суб-чутливий DC/VLF-дрейф: AC-вміст ~-40 dB RMS). Truncating cyclic crossfade прибирає шов петлі.
- `ambience.crickets` — truncating cyclic crossfade: шов 0.005 проти 0.11 у модульного wrap.
- `sfx.twig` — onset-рампа ~0.5 мс прибирає цифровий клік на t=0; DC-корекція прибрала зміщення.
- `sfx.rustle` — шов 0.047 не критичний (one-shot, не петля); хвіст фейдить до нуля.
- Worst-case peak кожного кліпа ≤ -1.9 dBFS; перекриття одночасних шарів контролюється бюджетом голосів (≤16) і MaxSimultaneous.

Резерв Kenney (click-a/b, switch-a/b, tap-a/b) — файли на диску, не задіяні в каталозі.
