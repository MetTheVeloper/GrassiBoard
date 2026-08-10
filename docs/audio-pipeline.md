# Audio pipeline

## v0.11.2 live route

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
Local media -> streaming decode -> bounded read-ahead            |
  -> native SPSC Media ring -------------------------------------+
  -> optional independent headphone monitor (Media only)         |
                                                               |
                     stereo sum -> Master Gain -> Limiter
                                -> Clipping Protection -> safety clamp
                                -> WASAPI external-cable render
                                -> paired recording endpoint
                                -> target application microphone
```

Sound Pads and Media enter after Voice Pitch/Formant and remain unpitched. Mic Mute gates only the microphone branch. Global Stop All clears Pads, stops Media, and stops the engine through the normal lifecycle path without deleting configuration.

## Real-time rules

The render callback may pop processed microphone and Media ring samples, drain bounded Pad commands, read cached Pad PCM, process fixed-size Mixer state, update atomic meters/counters, and write the WASAPI buffer. It does not know about files, decoders, NAudio, Profiles, hotkeys, Tray, or WPF.

It must not:

- open or read files;
- decode WAV/MP3;
- resample;
- stream/decode Media;
- allocate a clip or resize a collection;
- access WPF/UI state;
- acquire the clip-registry mutex;
- log or propagate exceptions.

## Meter definitions

- **MIC**: post-Pitch Wet/Dry, post-Mic Mute, post-Mic Gain/Gate/Compressor; before the stereo master bus.
- **BOARD**: post-Pad volume, post-Soundboard Gain and post-Ducking; before the stereo master bus.
- **MASTER**: final stereo output after Master Gain, Limiter, Clipping Protection, and safety clamp.
- **MEDIA**: local Media after its own volume and before the Master bus.

The WPF timer samples native statistics every 100 ms rather than repainting at audio-block frequency. Linear peaks are mapped to a clamped `-60..0 dBFS` display range; silence, NaN, and infinity map to an empty meter.

Diagnostics additionally expose Media ring fill/capacity/underruns and an estimated processing total. The estimate is not a physical loopback measurement.
