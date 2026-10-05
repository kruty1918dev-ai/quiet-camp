#!/usr/bin/env python3
"""Owned deterministic 48kHz Foley/chimes. No third-party samples; regenerate
files and inspect peaks/DC/clicks with --audit. Import through CozyAudioContent."""
import pathlib, wave, json, argparse
import numpy as np
from scipy.signal import butter, sosfilt
ROOT=pathlib.Path(__file__).resolve().parents[1]
DIR=ROOT/'QuietCamp/Assets/QuietCamp/Audio/Generated/Cozy'
RATE=48000

def noise(n,rng,cutoff):
 return sosfilt(butter(2,cutoff,fs=RATE,output='sos'),rng.uniform(-1,1,n))

def cue(kind,index):
 rng=np.random.default_rng(19181003+index*59+sum(map(ord,kind)))
 duration={'lift':.44,'settle':.34,'rotate':.24,'remove':.38,'page':.28,'hint':1.5,'pause':.65,'resume':.65,'toggle':.14,'check':.30}[kind]
 t=np.arange(int(duration*RATE))/RATE;n=len(t)
 if kind in ('hint','pause','resume'):
  freqs={'hint':[392,493.883,587.33],'pause':[392,329.628],'resume':[329.628,392]}[kind]
  s=np.zeros(n)
  for k,f in enumerate(freqs):
   q=np.maximum(0,t-k*.11);e=np.where(t>=k*.11,(1-np.exp(-q*90))*np.exp(-q*4),0)
   s+=(np.sin(2*np.pi*f*q)+.18*np.sin(2*np.pi*f*2.002*q)+.05*np.sin(2*np.pi*f*3.003*q))*e
 else:
  s=noise(n,rng,1400 if kind=='page' else 2800)*(.55+noise(n,rng,70)*.3)
  if kind in ('settle','rotate','remove','toggle','check'):
   s+=np.sin(2*np.pi*(120+index*7)*t)*np.exp(-t*35)*.17
  envelope=(1-np.exp(-t*(90 if kind=='lift' else 250)))*np.exp(-t*(6 if kind=='lift' else 12))
  if kind=='lift':envelope*=.7+.3*np.sin(np.pi*t/duration)
  s*=envelope
 return s

def write(name,s):
 s=np.asarray(s,dtype=np.float64);s-=np.mean(s,axis=0)
 edge=min(480,len(s)//8);fade=np.sin(np.linspace(0,np.pi/2,edge))**2
 s[:edge]*=fade[:,None] if s.ndim==2 else fade
 s[-edge:]*=fade[::-1,None] if s.ndim==2 else fade[::-1]
 window=np.sin(np.linspace(0,np.pi,len(s)))**2
 correction=np.sum(s,axis=0)/np.sum(window)
 s-=window[:,None]*correction if s.ndim==2 else window*correction
 s*=.72/max(.01,np.max(np.abs(s)))
 with wave.open(str(DIR/name),'wb') as f:
  f.setnchannels(2 if s.ndim==2 else 1);f.setsampwidth(2);f.setframerate(RATE);f.writeframes(np.round(s*32767).astype('<i2').tobytes())

def music():
 # Sparse, softly damped mallets: long quiet breaths between phrases.
 n=RATE*48;s=np.zeros((n,2))
 notes=[(1,196,0),(1.6,293.665,.25),(2.3,392,-.2),(13,220,-.15),(13.8,329.628,.2),(25,196,.2),(26.1,293.665,-.2),(37,164.814,-.2),(38,261.626,.15)]
 for start,f,pan in notes:
  t=np.arange(RATE*5)/RATE;env=(1-np.exp(-t*38))*np.exp(-t*1.5)
  note=(np.sin(2*np.pi*f*t)+.2*np.sin(2*np.pi*f*2.002*t)+.07*np.sin(2*np.pi*f*4.01*t))*env
  offset=int(start*RATE);end=min(n,offset+len(t));length=end-offset
  s[offset:end,0]+=note[:length]*np.sqrt((1-pan)/2);s[offset:end,1]+=note[:length]*np.sqrt((1+pan)/2)
 return s

def audit():
 out=[]
 for p in sorted(DIR.glob('*.wav')):
  with wave.open(str(p)) as f:
   c=f.getnchannels();a=np.frombuffer(f.readframes(f.getnframes()),'<i2').astype(float)/32768
   out.append(dict(file=p.name,rate=f.getframerate(),channels=c,seconds=round(len(a)/c/f.getframerate(),3),peak_dbfs=round(20*np.log10(max(1e-9,np.max(np.abs(a)))),2),dc=round(abs(a.mean()),7),first_last=max(abs(a[0]),abs(a[-1]))))
 assert all(x['rate']==RATE and x['peak_dbfs'] < -2 and x['dc']<.001 and x['first_last']<.001 for x in out)
 print(json.dumps(out,indent=2))

if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--audit',action='store_true');a=p.parse_args()
 if not a.audit:
  DIR.mkdir(parents=True,exist_ok=True)
  for kind in ('lift','settle','rotate','remove','page','hint','pause','resume','toggle','check'):
   for i in range(3 if kind in ('lift','settle','rotate','remove','page') else 1):write(f'{kind}_{i}.wav',cue(kind,i))
  write('clearing_music.wav',music())
 audit()
