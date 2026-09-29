"""Original 32-bar happy chiptune: 'Oops! All Chaos'. No sampled recordings.
Run with Python + numpy to rebuild Assets/Resources/Audio/GoofyShuffle.wav.
"""
from pathlib import Path
import wave
import numpy as np

RATE = 22050
BPM = 144
BEAT = 60 / BPM
BARS = 32
N = round(BARS * 4 * BEAT * RATE)
mix = np.zeros((N, 2), dtype=np.float64)
rng = np.random.default_rng(8261)

def put(sound, beat, level, pan=0):
    start = round(beat * BEAT * RATE)
    indices = (start + np.arange(len(sound))) % N  # Wrap tails for a seamless loop.
    gains = np.sqrt([(1-pan)/2, (1+pan)/2])
    mix[indices] += sound[:, None] * level * gains

def note(midi, beats, voice='lead', bend=0):
    t = np.arange(round(beats * BEAT * RATE)) / RATE
    f = 440 * 2 ** ((midi - 69) / 12)
    # Slightly unreliable toy keyboard: modest detuning and delayed vibrato.
    detune = rng.uniform(-11, 11) if voice == 'lead' else rng.uniform(-4, 4)
    wobble = .0028 * np.sin(2*np.pi*6.1*t) * np.minimum(1,t*7) if voice == 'lead' else 0
    phase = np.cumsum(f * 2**(detune/1200) * (1+wobble) * np.ones_like(t)) / RATE + bend * (1 - np.exp(-t * 40)) / 40
    if voice == 'bass':
        s = sum(((-1)**((h-1)//2) / h**2) * np.sin(2*np.pi*h*phase) for h in range(1, 12, 2))
    else:
        duty = 0.25 if voice == 'arp' else 0.5
        s = sum((np.sin(np.pi*h*duty)/h) * np.sin(2*np.pi*h*phase)
                for h in range(1, min(12, int(RATE/2/f))))
    attack = np.minimum(1, t / 0.004)
    release = np.minimum(1, (len(t)/RATE - t) / 0.025)
    decay = np.exp(-t * (7 if voice in ('arp', 'chord') else 2.3))
    return s * attack * release * decay

def drum(kind):
    duration = {'kick': .14, 'snare': .105, 'hat': .035, 'wood': .065}[kind]
    t = np.arange(round(duration*RATE))/RATE
    noise = rng.uniform(-1, 1, len(t))
    if kind == 'kick': s = np.sin(2*np.pi*(48*t + 4*(1-np.exp(-t*38)))) * np.exp(-t*28)
    elif kind == 'snare': s = (noise*.7 + np.sin(2*np.pi*185*t)*.3) * np.exp(-t*38)
    elif kind == 'wood': s = (np.sin(2*np.pi*780*t)+np.sin(2*np.pi*1190*t)*.4)*np.exp(-t*75)
    else: s = np.diff(noise, prepend=0)*np.exp(-t*100)*.45
    return s*np.minimum(1,t/.0015)*np.minimum(1,(duration-t)/.008)

def bloop(midi, duration=.21, variant=0):
    """Original high-register rubbery squeak, with a stepped pitch overshoot.
    Smooth envelopes keep the deliberately awkward sound free of hard clicks.
    """
    t = np.arange(round(duration*RATE))/RATE
    u = t/duration
    curve = np.interp(u, [0,.10,.22,.42,.72,1],
                     [-9, 5, 2, -1, 1, -6] if variant%2==0 else [7,-3,0,3,-2,-9])
    curve = np.round(curve*2)/2 + .22*np.sin(2*np.pi*18*t)
    frequency = 440 * 2**((midi-69+curve)/12)
    phase = np.cumsum(frequency)/RATE
    sound = np.sin(2*np.pi*phase) + .18*np.sin(4*np.pi*phase)
    sound = np.round(sound*23)/23  # Coarse toy-synth texture, not piercing noise.
    envelope = np.minimum(1,t/.006)*np.minimum(1,(duration-t)/.02)*np.exp(-u*2.1)
    return sound*envelope

# C major, a playful secondary dominant in A7, and a contrasting F-major bridge.
chords = [
    (48,[60,64,67,69]), (45,[61,64,67,69]), (50,[60,62,65,69]), (43,[59,62,65,67]),
    (48,[60,64,67,71]), (45,[60,64,67,69]), (53,[60,65,69,72]), (43,[59,62,65,67]),
]
bridge = [(53,[60,65,69,72]),(48,[60,64,67,72]),(50,[60,62,65,69]),(43,[59,62,65,67]),
          (52,[59,64,67,71]),(45,[60,64,69,72]),(50,[60,62,65,69]),(43,[59,62,65,67])]
# Each phrase is an original rhythm/melody in MIDI notes. None transcribed from references.
melodies = [
 [(0,76,.45),(.5,79,.22),(.78,81,.2),(1,79,.4),(1.5,76,.3),(2.25,72,.3),(2.75,74,.2),(3,76,.65)],
 [(0,73,.25),(.5,76,.3),(1,81,.5),(1.75,79,.2),(2,76,.4),(2.75,73,.2),(3.25,76,.4)],
 [(0,77,.45),(.5,81,.22),(.78,84,.2),(1.25,81,.3),(2,77,.4),(2.5,76,.22),(3,74,.7)],
 [(.25,74,.22),(.75,77,.22),(1.25,79,.5),(2,83,.25),(2.5,81,.22),(3,79,.22),(3.5,77,.25)],
 [(0,76,.22),(.5,79,.22),(1,84,.7),(2,83,.22),(2.5,79,.22),(3,76,.6)],
 [(0,81,.45),(.75,79,.2),(1,76,.4),(1.75,72,.2),(2.25,76,.3),(3,79,.22),(3.5,81,.22)],
 [(0,81,.25),(.5,77,.25),(1.25,76,.2),(1.75,77,.25),(2.5,81,.25),(3,84,.55)],
 [(0,83,.22),(.5,81,.22),(1,79,.4),(1.75,77,.2),(2.25,74,.3),(3,71,.3),(3.5,74,.22)],
]
for bar in range(BARS):
    section = bar//8
    root, chord = (bridge if section==2 else chords)[bar%8]
    base = bar*4
    # Alternating bass and offbeat chord stabs give the deliberately silly bounce.
    for b, pitch in [(0,root), (1,root+12), (2,root+7), (3,root+12)]:
        put(note(pitch,.42,'bass'),base+b+(0.018 if b%2 else 0),.26)
    for b in [.5,1.5,2.5,3.5]:
        for k,pitch in enumerate(chord[:3]): put(note(pitch,.22,'chord'),base+b+k*.017,.042,-.28)
    for j in range(8):
        beat = j*.5 + (.055 if j%2 else 0)
        put(note(chord[[0,2,1,3][j%4]]+12,.18,'arp'),base+beat,.044,.38)
        put(drum('hat'),base+beat,.057,-.35 if j%2 else .35)
    for b in [0,2]: put(drum('kick'),base+b,.3)
    for b in [1,3]: put(drum('snare'),base+b,.13)
    if bar%4==3:
        for j in range(4): put(drum('wood'),base+3+j*.25,.08,(-1)**j*.4)
    if section==2:
        phrase=[(0,chord[2]+12,.65),(1,chord[1]+12,.3),(1.5,chord[0]+12,.3),
                (2.25,chord[1]+12,.3),(2.75,chord[2]+12,.3),(3.5,chord[3]+12,.25)]
    else: phrase=melodies[bar%8]
    for index,(offset,pitch,length) in enumerate(phrase):
        if section==1 and index==0: pitch-=12  # Call/response octave joke.
        signal=note(pitch,length,'lead',bend=14 if bar%4==1 and index==0 else 0)
        late = rng.uniform(-.012,.038) if offset else .015
        put(signal,base+offset+late,.16*rng.uniform(.88,1.1),-.08)
        put(signal,base+offset+late+.75,.022,.5)  # Quiet chip echo, also wrapped.
        if bar%8==5 and index==2:
            # A little wrong-note grace that immediately corrects itself.
            put(note(pitch+1,.075,'lead'),base+offset-.08,.07,.1)
    if section==3 and bar%2==0:
        for j in range(3): put(note(chord[j]+24,.12,'arp'),base+3.25+j*.25,.055,.35)
    # Recurring bloop character: a question, a late answer, and a bridge solo.
    # Register stays around C6-G6; very high harmonics are deliberately subdued.
    if bar%2==0:
        put(bloop(84+(bar%3)*2,.20,bar),base+1.84,.125,.22)
    else:
        put(bloop(88,.17,bar),base+3.12,.12,-.2)
        if bar%4==3: put(bloop(84,.25,bar+1),base+3.64,.10,.28)
    if section==2 and bar%2==0:
        for j,pitch in enumerate([84,88,86,91]):
            put(bloop(pitch,.13,j),base+2.15+j*.32,.095,(-1)**j*.25)
    if section==3 and bar%4==2:
        # A tiny stutter reply rather than the same sample every time.
        for j in range(3): put(bloop(86+j,.08,j),base+3.18+j*.18,.085,.15)

mix -= np.mean(mix,axis=0)
# Gentle saturation, peak headroom and exact loop length. No silent fade at the join.
mix = np.tanh(mix*1.2)
mix *= .78 / np.max(np.abs(mix))
pcm = np.rint(mix*32767).astype('<i2')
out=Path(__file__).resolve().parents[1]/'Assets/Resources/Audio/GoofyShuffle.wav'
out.parent.mkdir(parents=True,exist_ok=True)
with wave.open(str(out),'wb') as wav:
    wav.setnchannels(2); wav.setsampwidth(2); wav.setframerate(RATE); wav.writeframes(pcm.tobytes())
assert np.isfinite(mix).all() and np.max(np.abs(mix)) < 1
seam=float(np.max(np.abs(mix[0]-mix[-1])))
print(f'{out}\n{N/RATE:.3f}s | {BPM} BPM | stereo | peak {np.max(np.abs(mix)):.3f} | RMS {np.sqrt(np.mean(mix**2)):.3f} | seam step {seam:.5f}')
assert seam < .06, 'Loop boundary discontinuity'
