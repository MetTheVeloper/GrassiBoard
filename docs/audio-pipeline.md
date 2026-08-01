# Audio pipeline

Milestone 1 activates the first user-mode audio path:

```text
Selected physical microphone
    ↓ WASAPI shared/event-driven capture
48 kHz mono float
    ↓ preallocated ring buffer
48 kHz stereo duplication
    ↓ WASAPI shared/event-driven render
Selected headset monitoring output
```

Windows Audio performs endpoint format conversion and resampling where the physical device mix format differs from the requested internal format.

No pitch, formant, noise processing, mixer, soundboard, virtual output, or kernel driver participates in this version.
