# Forest lighting source

Sunset Forest HDRI by Andreas Mischok, Poly Haven.

- Asset: https://polyhaven.com/a/sunset_forest
- License: CC0 1.0, https://polyhaven.com/license and https://creativecommons.org/publicdomain/zero/1.0/
- Original download: https://dl.polyhaven.org/file/ph-assets/HDRIs/hdr/1k/sunset_forest_1k.hdr
- Downloaded 2026-10-03, 1,843,297 bytes.
- SHA256: `189502addad02ba43449b895de66bcdf115e7150536d9150d9a7ba05e1d15b11`

Unmodified source stays in `Editor/` and is excluded from the player. `Quiet Camp > Build Forest Lighting` integrates the environment into 27 normalized diffuse spherical-harmonics coefficients. Bright samples are capped to avoid counting the direct sun twice. The scene atmosphere tints the probe for morning, noon, evening, night and clouds. Source texture and third-party preview images are not used as a game background.

The localized shadow/depth-aware sun shafts are game code; no external renderer feature or extra camera is needed.
