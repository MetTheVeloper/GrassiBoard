# Audio pipeline

## v0.9.0 live route

```text
Selected physical microphone
  -> WASAPI shared/event-driven capture (48 kHz mono float)
  -> prewarmed Signalsmith Pitch/Fine Pitch/Formant processors
  -> latency-aligned Pitch Wet/Dry mix
  -> preallocated mono ring
  -> Mic Mute
  -> Mic Gain -> Noise Gate -> Compressor -------------------+
                                                               |
WAV / MP3 -> background decode/resample -> immutable cache      |
  -> fixed command queue + 32-voice Soundboard mixer            |
  -> Soundboard Gain -> microphone-keyed Ducking ---------------+
                                                               |
                     stereo sum -> Master Gain -> Limiter
                                -> Clipping Protection -> safety clamp
                                -> WASAPI external-cable render
                                -> paired recording endpoint
                                -> target application microphone
```

Sound Pads enter after Voice Pitch/Formant and remain unpitched. Mic Mute gates only the microphone branch. Global Stop All clears every Pad voice and stops the engine through the normal lifecycle path, but does not change routing, Voice, Mixer, presets, or Board configuration.

## Real-time rules

The render callback may pop processed microphone samples, drain bounded playback commands, read cached PCM, process fixed-size Mixer/Dynamics state, update atomic meters/counters, and write the WASAPI buffer. UI changes publish one validated settings structure through atomics; the callback reads it at block boundaries. Gain targets and dynamics envelopes are smoothed.

It must not:

- open or read files;
- decode WAV/MP3;
- resample;
- allocate a clip or resize a collection;
- access WPF/UI state;
- acquire the clip-registry mutex;
- log or propagate exceptions.

## Meter definitions

- **MIC**: post-Pitch Wet/Dry, post-Mic Mute, post-Mic Gain/Gate/Compressor; before the stereo master bus.
- **BOARD**: post-Pad volume, post-Soundboard Gain and post-Ducking; before the stereo master bus.
- **MASTER**: final stereo output after Master Gain, Limiter, Clipping Protection, and safety clamp.

The WPF timer samples native statistics every 100 ms rather than repainting at audio-block frequency. Linear peaks are mapped to a clamped `-60..0 dBFS` display range; silence, NaN, and infinity map to an empty meter.

## Deferred work

Separate low-latency headphone monitoring remains deferred. It requires a second render route and a separate latency policy and is intentionally outside v0.9.0.
