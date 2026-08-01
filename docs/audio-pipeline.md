# Audio pipeline

Milestone 2 activates the first real-time DSP path:

```text
Selected physical microphone
    -> WASAPI shared/event-driven capture
48 kHz mono float
    -> Signalsmith pitch processor or latency-aligned Bypass
    -> preallocated mono ring buffer
    -> stereo duplication
    -> WASAPI shared/event-driven render
Selected headset monitoring output
```

Pitch and Fine Pitch targets are updated without restarting the stream. The processor applies 25 ms parameter smoothing, while Bypass uses a 10 ms crossfade between latency-aligned dry and wet signals. The UI reports the algorithmic pitch latency in samples and milliseconds separately from endpoint buffer and dropout diagnostics.

Windows Audio performs endpoint-format conversion and resampling where the physical device mix format differs from the requested internal format.

Formant processing, selectable quality modes, noise processing, mixer, soundboard, virtual output, and kernel driver do not participate in this version.
