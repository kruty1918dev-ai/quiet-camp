"""Owned, sample-free 48 kHz score: question, discovery, space, resolution. No third-party music."""
import argparse
from pathlib import Path
import wave
import numpy as np


def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--output',type=Path,required=True);a=p.parse_args()
    rate=48000;duration=16;n=rate*duration;s=np.zeros((n,2),dtype=np.float64)
    # Sparse D-major pentatonic. Gaps leave the real rotation/settle/check cues audible.
    notes=[(.13,293.665,.07),(.9,440,.04),(2.55,329.628,.055),(3.4,440,.04),
           (4.9,587.33,.055),(6.1,493.883,.035),(7.9,440,.05),(9.0,329.628,.04),
           (10.25,392,.04),(11.1,440,.035),(12.75,293.665,.07),(12.9,440,.04),
           (13.05,587.33,.045),(14.35,659.255,.025),(14.8,587.33,.025)]
    for i,(at,f,gain) in enumerate(notes):
        start=round(at*rate);length=min(n-start,round(2.1*rate));t=np.arange(length)/rate
        env=(1-np.exp(-65*t))*np.exp(-2.9*t)
        tone=(np.sin(2*np.pi*f*t)+.15*np.sin(2*np.pi*2.003*f*t))*env*gain
        pan=.18 if i%2 else -.18
        s[start:start+length,0]+=tone*(1-pan);s[start:start+length,1]+=tone*(1+pan)
        for echo,amount in [(.18,.14),(.31,.07)]:
            q=start+round(echo*rate);limit=min(length,n-q)
            if limit>0:s[q:q+limit]+=tone[:limit,None]*amount
    s*=np.minimum(1,np.maximum(0,(15.8-np.arange(n)/rate)/.55))[:,None]
    a.output.parent.mkdir(parents=True,exist_ok=True)
    with wave.open(str(a.output),'wb') as out:
        out.setnchannels(2);out.setsampwidth(2);out.setframerate(rate);out.writeframes(np.round(np.clip(s,-1,1)*32767).astype('<i2').tobytes())
    print('Owned score:',len(s),'stereo samples, peak',round(float(np.max(np.abs(s))),4))


if __name__=='__main__':main()
