# Audio pipeline

## Live route

```text
Selected physical microphone
    -> WASAPI shared/event-driven capture
    -> 48 kHz mono float
    -> prewarmed Signalsmith Voice DSP
       Pitch + Fine Pitch + Formant + preservation + quality crossfade
    -> existing preallocated mono ring buffer
    -> Mic Mute gate --------------------------------------┐
                                                          ├-> bounded stereo master sum
WAV / MP3 file                                            │       -> WASAPI render
    -> background decode + channel conversion + resample  │       -> external cable playback endpoint
    -> immutable cached 48 kHz stereo float clip           │       -> paired cable recording endpoint
    -> fixed command queue + 32-voice Soundboard mixer ----┘       -> target application microphone
```

The Voice branch is the accepted v0.7.0 path. Soundboard enters after Pitch/Formant processing, so Pad audio stays at its original pitch. Mic Mute gates only the Voice branch. Stop All clears only Soundboard voices.

## Real-time rules

The render callback may pop processed microphone samples, drain bounded playback commands, read cached PCM, mix active voices, update atomic meters/counters, and write the WASAPI buffer.

It must not:

- open or read files;
- decode WAV/MP3;
- resample;
- allocate a clip or resize a collection;
- access WPF/UI state;
- acquire the clip-registry mutex;
- log or propagate exceptions.

## Meter definitions

- **Mic**: physical microphone peak received by capture. It may continue moving while Mic Mute is active.
- **Soundboard**: summed Pad peak before the master clamp.
- **Master**: final stereo signal written to the external virtual-cable playback endpoint.

The WPF timer samples native statistics every 100 ms rather than repainting at audio-block frequency.

## Deferred work

Separate low-latency headphone monitoring, gain stages, dynamics, ducking, and configurable clipping protection remain future milestones. The current master uses bounded float clamping only when Voice and Soundboard sums exceed the output range; the accepted microphone-only route remains unchanged below that boundary.
