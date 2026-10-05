#!/usr/bin/env python3
"""Own, deterministic, periodic soft rain bed; no downloaded audio."""
from pathlib import Path
import numpy as np
import re, uuid, wave
root=Path(__file__).resolve().parents[1]/'QuietCamp'
folder=root/'Assets/QuietCamp/Audio/Generated'
rate=22050; count=rate*12; rng=np.random.default_rng(19181003)
frequency=np.fft.rfftfreq(count,1/rate)
shaping=(1-np.exp(-frequency/150))*np.exp(-frequency/4500)/np.sqrt(np.maximum(frequency,250))
samples=np.fft.irfft(np.fft.rfft(rng.normal(size=count))*shaping,n=count)
samples*=.10/np.sqrt(np.mean(samples*samples))
samples=np.clip(samples,-.7,.7)
path=folder/'gentle_rain.wav'
with wave.open(str(path),'wb') as wav:
    wav.setnchannels(1);wav.setsampwidth(2);wav.setframerate(rate);wav.writeframes((samples*32767).astype('<i2').tobytes())
meta=Path(str(path)+'.meta')
if not meta.exists():
    template=(folder/'wind_loop.wav.meta').read_text()
    meta.write_text(re.sub(r'guid: [0-9a-f]+','guid: '+uuid.uuid4().hex,template).replace('normalize: 1','normalize: 0'))
guid=re.search(r'guid: ([0-9a-f]+)',meta.read_text()).group(1)
catalog=root/'Assets/QuietCamp/Resources/QuietCamp/AudioCatalog.asset';text=catalog.read_text()
if '  - Key: ambience.rain\n' not in text:
    source=re.search(r'  - Key: ambience.crickets\n.*?(?=  - Key:)',text,re.S).group(0)
    entry=source.replace('ambience.crickets','ambience.rain')
    entry=re.sub(r'guid: [0-9a-f]+','guid: '+guid,entry)
    entry=re.sub(r'    Volume: [0-9.]+','    Volume: 0.18',entry)
    text=text.replace('  _channels:',entry+'  _channels:')
    catalog.write_text(text)
print('Generated 12 s periodic rain, streaming Vorbis, ambience bus.')
