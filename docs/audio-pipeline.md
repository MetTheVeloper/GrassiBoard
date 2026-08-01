# Audio pipeline

Milestone 3 uses this user-mode path:

```text
Selected physical microphone
    -> WASAPI shared/event-driven capture
48 kHz mono float
    -> three pre-warmed Signalsmith configurations
       Low latency / Balanced / High quality
    -> 20 ms live mode crossfade
    -> formant-aware pitch output or latency-aligned Bypass
    -> preallocated mono ring buffer
    -> stereo duplication
    -> WASAPI shared/event-driven render
Selected headset monitoring output
```

Pitch, Fine Pitch, Formant Shift, preservation, Bypass, and quality targets update without restarting the stream. The UI reports the selected mode's algorithmic latency after its crossfade completes. Endpoint and ring-buffer delays remain separate.

Formant preservation is implemented as a smoothly variable compensation term tied to the current pitch map. Formant Shift remains independent, so the user can preserve vocal character and then deliberately move it.

Noise processing, mixer, soundboard, virtual output, and kernel driver do not participate in this version.
