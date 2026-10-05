#!/usr/bin/env python3
"""Deterministic owned biome beds and surface Foley; no downloaded samples or Unity build.

Writes only Biomes audio and its registered catalog keys. --audit is read-only.
PCM checks cover levels, DC, finite samples and joins, not perceptual validation.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re
import uuid
import wave
import numpy as np
from scipy.signal import butter, sosfilt
from camp_soundscape_audio import band_noise, periodic_add

ROOT = Path(__file__).resolve().parents[1]
FOLDER = ROOT / "QuietCamp/Assets/QuietCamp/Audio/Generated/Biomes"
CATALOG = ROOT / "QuietCamp/Assets/QuietCamp/Resources/QuietCamp/AudioCatalog.asset"
RATE = 48000
BEDS = ("forest", "meadow", "autumn", "winter", "water")
SURFACES = ("snow", "leaves", "mud", "soil", "grass")


def bed(kind):
    rng = np.random.default_rng(71019 + BEDS.index(kind))
    n = RATE * 12
    t = np.arange(n) / RATE
    result = []
    for channel in range(1 if kind == "water" else 2):
        low, high = {"forest": (190, 1900), "meadow": (340, 2300), "autumn": (460, 3500),
                     "winter": (85, 650), "water": (230, 1800)}[kind]
        signal = band_noise(n, rng, low, high)
        # Integer-period envelopes and circular grains make a genuine seamless loop.
        envelope = .20 + .09 * np.sin(2*np.pi*t/12 + channel*.7)**2 + .06*np.sin(2*np.pi*t/4)**2
        signal *= envelope
        if kind == "water":
            for onset in rng.uniform(0, 12, 52):
                clock = np.arange(int(RATE*.22))/RATE
                grain = np.sin(2*np.pi*(rng.uniform(360, 750)*clock+240*clock**2))
                grain *= np.sin(np.pi*clock/.22)**2 * np.exp(-clock*19)*.12
                periodic_add(signal, onset, grain)
        elif kind != "winter":
            for onset in rng.uniform(0,12,20 if kind == "autumn" else 10):
                clock = np.arange(int(RATE*.35))/RATE
                grain = sosfilt(butter(2,high,fs=RATE,output="sos"),rng.normal(size=len(clock)))
                periodic_add(signal,onset,grain*np.sin(np.pi*clock/.35)**2*.16)
        result.append(signal)
    return result[0] if len(result)==1 else np.column_stack(result)


def surface(kind, variant):
    rng = np.random.default_rng(99173 + SURFACES.index(kind)*37 + variant)
    n = int(RATE*(.48+variant*.03)); t = np.arange(n)/RATE
    cutoff = {"snow": 3400,"leaves": 4900,"mud": 900,"soil": 1900,"grass": 2400}[kind]
    noise = sosfilt(butter(2,cutoff,fs=RATE,output="sos"),rng.normal(size=n))
    envelope = (1-np.exp(-t*85))*np.exp(-t*(12 if kind in ("snow","leaves") else 19))
    impact = np.sin(2*np.pi*(72+variant*4)*t)*(1-np.exp(-t*160))*np.exp(-t*38)
    signal = noise*envelope*.65 + impact*(.12 if kind=="leaves" else .30)
    if kind in ("snow","leaves","soil"):
        for onset in rng.uniform(.035,.22,28 if kind=="snow" else 18):
            grain_time = np.arange(400)/RATE
            grain = rng.normal(size=400)*np.sin(np.linspace(0,np.pi,400))**2
            periodic_add(signal,onset,grain*.10*np.exp(-onset*6))
    return signal


def guid(name):
    return uuid.uuid5(uuid.NAMESPACE_URL,"quiet-camp-owned-biome-audio/"+name).hex


def write(name, signal, loop):
    signal = signal.astype(float); signal -= np.mean(signal,axis=0)
    if not loop:
        edge=480; fade=np.sin(np.linspace(0,np.pi/2,edge))**2
        signal[:edge]*=fade; signal[-edge:]*=fade[::-1]
        window=np.sin(np.linspace(0,np.pi,len(signal)))**2
        signal-=window*np.sum(signal)/np.sum(window)
    signal *= (.32 if loop else .50)/max(.001,np.max(np.abs(signal)))
    path=FOLDER/name
    with wave.open(str(path),"wb") as file:
        file.setnchannels(1 if signal.ndim==1 else 2);file.setsampwidth(2);file.setframerate(RATE)
        file.writeframes(np.round(signal*32767).astype("<i2").tobytes())
    template=ROOT/"QuietCamp/Assets/QuietCamp/Audio/Generated/Soundscape"/("rain_canopy.wav.meta" if loop else "drip_0.wav.meta")
    meta=template.read_text()
    meta=re.sub(r"(?m)^guid: .*", "guid: "+guid(name),meta)
    meta=re.sub(r"(?m)^  forceToMono: .*","  forceToMono: "+str(int(signal.ndim==1)),meta)
    meta=re.sub(r"(?m)^  normalize: .*","  normalize: 0",meta)
    if not Path(str(path)+".meta").exists():Path(str(path)+".meta").write_text(meta)


def register_catalog():
    text=CATALOG.read_text()
    def entry(key):
        return re.search(r"(?ms)^  - Key: "+re.escape(key)+r"\n.*?(?=^  - Key:|^  _|\Z)",text).group()
    loop_template=entry("ambience.rain")
    surface_template=entry("sfx.tent.drag")
    additions=[]
    for name in BEDS+SURFACES:
        loop=name in BEDS;key=("ambience.biome." if loop else "sfx.surface.")+name
        files=[name+".wav"] if loop else [f"{name}_{i}.wav" for i in range(3)]
        block=loop_template if loop else surface_template
        block=re.sub(r"(?m)^  - Key: .*","  - Key: "+key,block)
        block=re.sub(r"(?m)^    Clip: .*",f"    Clip: {{fileID: 8300000, guid: {guid(files[0])}, type: 3}}",block)
        variants="    Variants: []\n" if loop else "    Variants:\n"+"".join(f"    - {{fileID: 8300000, guid: {guid(file)}, type: 3}}\n" for file in files)
        block=re.sub(r"(?m)^    Variants:.*\n(?:    - .*\n)*",variants,block)
        fields={"Bus":4 if loop else 2,"Volume":.12 if name=="water" else .08 if loop else .13,
                "SpatialBlend":int(not loop or name=="water"),"Cooldown":0 if loop else .20,
                "MinDistance":3,"MaxDistance":28 if name=="water" else 18,"PoolWarmup":0}
        for field,value in fields.items():block=re.sub(r"(?m)^    "+field+r": .*",f"    {field}: {value}",block)
        match=re.search(r"(?ms)^  - Key: "+re.escape(key)+r"\n.*?(?=^  - Key:|^  _|\Z)",text)
        if match:text=text[:match.start()]+block+text[match.end():]
        else:additions.append(block)
    text=text.replace("  _channels:","".join(additions)+"  _channels:",1)
    CATALOG.write_text(text)


def audit():
    rows=[]
    for path in sorted(FOLDER.glob("*.wav")):
        with wave.open(str(path)) as file:
            samples=np.frombuffer(file.readframes(file.getnframes()),"<i2").astype(float).reshape(-1,file.getnchannels())/32768
            loop=path.stem in BEDS;seam=float(np.max(np.abs(samples[-1]-samples[0])))
            typical=float(np.percentile(np.abs(np.diff(samples,axis=0)),99.9))
            row=dict(file=path.name,rate=file.getframerate(),channels=file.getnchannels(),seconds=len(samples)/RATE,
                     peak_dbfs=float(20*np.log10(max(1e-9,np.max(np.abs(samples))))),
                     rms_dbfs=float(20*np.log10(max(1e-9,np.sqrt(np.mean(samples*samples))))),
                     dc=float(np.max(np.abs(samples.mean(axis=0)))),loop=loop,seam=seam,
                     sha256=hashlib.sha256(path.read_bytes()).hexdigest())
            assert row["rate"]==48000 and row["peak_dbfs"] < -5 and row["dc"] < .001
            assert seam<=max(.001,typical*2) if loop else np.max(np.abs(samples[[0,-1]]))<.001
            rows.append(row)
    assert len(rows)==20,len(rows)
    return rows


if __name__=="__main__":
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument("--audit",action="store_true")
    args=parser.parse_args()
    if not args.audit:
        FOLDER.mkdir(parents=True,exist_ok=True)
        if not Path(str(FOLDER)+".meta").exists():Path(str(FOLDER)+".meta").write_text("fileFormatVersion: 2\nguid: "+guid("folder")+"\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n")
        for name in BEDS:write(name+".wav",bed(name),True)
        for name in SURFACES:
            for variant in range(3):write(f"{name}_{variant}.wav",surface(name,variant),False)
        register_catalog()
    print(json.dumps(audit(),indent=2))
