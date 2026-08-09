# Soundboard

## User behavior

- Add one or many WAV/MP3 files with the Add button or drag/drop.
- Press Play to trigger a Pad; different Pads can play simultaneously.
- Set per-pad volume and Loop.
- Stop one Pad or use global Stop All.
- Edit title, source file, volume, Loop, and restart-on-press behavior.
- Remove a Pad without deleting its original audio file.

Pad definitions are restored at startup from `%APPDATA%\GrassiBoard\soundboard.json`. Source audio is referenced, not copied into the configuration. A moved, deleted, unsupported, unreadable, or empty file produces a visible Pad error that can be repaired through Edit.

## Decode/cache contract

NAudio 2.3.0 decodes supported files on a background thread. Mono input is expanded to stereo and non-48 kHz input is resampled to 48 kHz. The first milestone accepts mono/stereo files up to ten minutes. Completed float PCM is copied into native immutable cache storage outside the render callback.

The native mixer supports 32 simultaneous voices and a bounded 256-command queue. Pressing a Pad normally restarts its current instance; users may disable restart-on-press in Edit to layer repeated triggers.

## Mix contract

Soundboard audio mixes after the microphone Voice DSP and before the existing virtual output. It is never Pitch/Formant shifted. The microphone continues while Pads play. Mute Mic preserves Pads; Stop All preserves the microphone and engine.

Optional direct headphone monitoring is not implemented in v0.8.0 because it would require a separate render route and additional latency policy. Target-application monitoring may still be used where acceptable.
