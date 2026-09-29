# Shopping Cart Parade

The current original soundtrack replaces the previous chiptune with a goofy,
acoustic-style toy band: synthesized marimba, plucked strings, upright-style
bass, a soft low reed bridge, brushed drums and wooden knocks. New melody and
harmony in F major, with a swung rhythm and layered variations across 32 bars.
No square-wave lead, bit crushing or high pitched bloop effects. Instruments are
synthesized approximations, not live recordings or samples from the references.

53.333 seconds, 144 BPM, stereo 22,050 Hz PCM. Peak 0.78, RMS 0.163,
loop-boundary sample step 0.00240. Note and room-reflection tails wrap across the
loop boundary. All 32 bars checked for signal; no clipping.

Audio remains Assets/Resources/Audio/GoofyShuffle.wav so existing game references
continue to work. Run Tools/compose_soundtrack.py (Python + numpy) to regenerate;
Tools/compose_toy_band.py contains the arrangement and synthesis. Previous
chiptune source is retained in Tools/MusicArchive for reference only.

Playback starts automatically through GoofySoundtrack. Bottom-left volume slider,
M to mute, music ducking during player gunfire and continuous playback across
encounter resets are unchanged. No global audio or gameplay timing changes.