"""Original toy-band shuffle, 'Shopping Cart Parade'. No samples or chip waves."""
from pathlib import Path
import wave
import numpy as np
RATE,BPM=22050,144
BEAT=60/BPM
N=round(128*BEAT*RATE)
mix=np.zeros((N,2)); rng=np.random.default_rng(9192)
def put(sound,beat,gain,pan=0):
    start=round(beat*BEAT*RATE); gains=np.sqrt([(1-pan)/2,(1+pan)/2])
    for delay,level in [(0,1),(.047,.085),(.093,.05)]:
        mix[(start+round(delay*RATE)+np.arange(len(sound)))%N]+=sound[:,None]*gain*level*gains
def note(midi,beats,voice):
    t=np.arange(round(beats*BEAT*RATE))/RATE
    f=440*2**((midi-69+rng.uniform(-.045,.045))/12)
    if voice=='wood':
        s=np.sin(2*np.pi*f*t)*np.exp(-t*5)+.3*np.sin(2*np.pi*f*3.99*t)*np.exp(-t*19)+.12*np.sin(2*np.pi*f*9.98*t)*np.exp(-t*33)
    elif voice=='pluck':
        s=sum(np.sin(2*np.pi*f*h*(1+.00012*h*h)*t)/h**1.65*np.exp(-t*(5+h*1.6)) for h in range(1,9))
    elif voice=='bass':
        s=sum(np.sin(2*np.pi*f*h*t)/h**2*np.exp(-t*(3+h)) for h in range(1,5))
        s+=rng.uniform(-1,1,len(t))*.045*np.exp(-t*65)
    else:
        phase=2*np.pi*f*(t+.00025*np.sin(2*np.pi*5*t))
        s=sum(np.sin(h*phase)/h**2.2 for h in range(1,7))*np.exp(-t*2)
    return s*np.minimum(1,t/.006)*np.minimum(1,(len(t)/RATE-t)/.035)
def drum(kind):
    duration={'kick':.18,'brush':.13,'shaker':.07,'knock':.10}[kind]
    t=np.arange(round(duration*RATE))/RATE; noise=rng.uniform(-1,1,len(t))
    if kind=='kick': s=np.sin(2*np.pi*(56*t+.9*(1-np.exp(-t*30))))*np.exp(-t*24)
    elif kind=='brush': s=np.convolve(noise,[.2,.3,.3,.2],'same')*np.exp(-t*25)
    elif kind=='knock': s=(np.sin(2*np.pi*690*t)+.4*np.sin(2*np.pi*1070*t))*np.exp(-t*70)
    else: s=np.diff(noise,prepend=0)*np.exp(-t*50)*.25
    return s*np.minimum(1,t/.003)*np.minimum(1,(duration-t)/.015)
changes=[(41,[57,60,65]),(38,[57,62,65]),(43,[58,62,67]),(36,[58,60,64]),(41,[57,60,65]),(45,[57,61,64]),(38,[57,62,65]),(36,[58,60,64])]
bridge=[(46,[58,62,65]),(47,[59,62,65]),(41,[57,60,65]),(38,[57,62,65]),(43,[58,62,67]),(36,[58,60,64]),(41,[57,60,65]),(36,[58,60,64])]
phrases=[
[(0,69,.45),(.66,72,.25),(1,77,.65),(2,76,.25),(2.66,72,.25),(3,69,.65)],
[(.33,69,.25),(1,65,.45),(1.66,62,.25),(2.33,65,.35),(3,69,.55)],
[(0,70,.45),(.66,74,.25),(1.33,79,.45),(2.33,77,.3),(3,74,.65)],
[(0,76,.45),(1,74,.25),(1.66,70,.25),(2.33,67,.35),(3.33,64,.35)],
[(0,65,.25),(.66,69,.25),(1.33,72,.25),(2,77,.8),(3.33,76,.3)],
[(0,73,.45),(.66,76,.25),(1.33,69,.5),(2.33,73,.3),(3,76,.6)],
[(0,77,.5),(1,74,.3),(1.66,69,.25),(2.33,65,.35),(3,62,.6)],
[(.33,64,.25),(1,67,.4),(1.66,70,.25),(2.33,72,.5),(3.33,67,.3)]]
for bar in range(32):
    section=bar//8; root,chord=(bridge if section==2 else changes)[bar%8]; base=bar*4
    for offset,pitch in [(0,root),(1,root+12),(2,root+7),(3,root+12)]: put(note(pitch,.6,'bass'),base+offset,.26)
    for offset in [.66,1.66,2.66,3.66]:
        for k,pitch in enumerate(chord): put(note(pitch,.55,'pluck'),base+offset+k*.023,.07,-.3)
    for offset in [0,2]: put(drum('kick'),base+offset,.21)
    for offset in [1,3]: put(drum('brush'),base+offset+.035,.15,.15)
    for j in range(8): put(drum('shaker'),base+j//2+(0 if j%2==0 else .66),.065,(-1)**j*.35)
    phrase=phrases[bar%8]
    if section==2: phrase=[(0,chord[2]+12,.65),(1.33,chord[1]+12,.4),(2.33,chord[0]+12,.6),(3.33,chord[1]+12,.3)]
    for offset,pitch,length in phrase:
        voice='reed' if section==2 else 'wood'; pitch-=12 if section==2 else 0
        put(note(pitch,length+.2,voice),base+offset+rng.uniform(.006,.035),.13 if section==2 else .19,-.08)
    if bar%2==1:
        for j in range(3): put(drum('knock'),base+3.15+j*.24,.045,.3)
    if section in (1,3):
        for j in range(4): put(note(chord[j%3]+12,.3,'pluck'),base+j+.66,.055,.35)
    if section==3 and bar%4==2:
        for j,pitch in enumerate([77,76,74,72]): put(note(pitch,.3,'wood'),base+2.66+j*.3,.085,.25)
mix-=mix.mean(axis=0)
mix=np.tanh(mix*1.15); mix*=.78/np.max(np.abs(mix))
seam=float(np.max(np.abs(mix[0]-mix[-1])))
assert np.isfinite(mix).all() and seam<.01
assert min(np.sqrt(np.mean(b*b)) for b in np.array_split(mix,32))>.025
out=Path(__file__).resolve().parents[1]/'Assets/Resources/Audio/GoofyShuffle.wav'
with wave.open(str(out),'wb') as w:
    w.setnchannels(2); w.setsampwidth(2); w.setframerate(RATE); w.writeframes(np.rint(mix*32767).astype('<i2').tobytes())
print(f'{N/RATE:.3f}s | peak {np.max(np.abs(mix)):.3f} | RMS {np.sqrt(np.mean(mix**2)):.3f} | loop boundary {seam:.5f}')
