# Soundboard

## User behavior

- Add one or many WAV/MP3 files with the Add button or drag/drop.
- Press Play to trigger a Pad; different Pads can play simultaneously.
- Set per-pad volume and Loop.
- Stop one Pad or use global Stop All.
- Edit title, source file, volume, Loop, and restart-on-press behavior.
- Optionally assign a global hotkey to each Pad.
- Remove a Pad without deleting its original audio file.

Pad definitions are restored from the active Profile in `%APPDATA%\GrassiBoard\profiles.json`; the legacy `soundboard.json` is migrated once. Source audio is referenced, not copied. A moved, deleted, unsupported, unreadable, or empty file produces a visible Pad error that can be repaired through Edit.

## Decode/cache contract

NAudio 2.3.0 decodes supported files on a background thread. Mono input is expanded to stereo and non-48 kHz input is resampled to 48 kHz. The first milestone accepts mono/stereo files up to ten minutes. Completed float PCM is copied into native immutable cache storage outside the render callback.

The native mixer supports 32 simultaneous voices and a bounded 256-command queue. Pressing a Pad normally restarts its current instance; users may disable restart-on-press in Edit to layer repeated triggers.

## Mix contract

Soundboard audio mixes after the microphone Voice DSP and is never Pitch/Formant shifted. Soundboard Gain and optional microphone-keyed Ducking apply before the master bus. The microphone continues while Pads play and Mute Mic preserves Pads. In v0.11 Global Stop All clears Pads, stops Media Deck, and safely stops the engine without deleting Profile, Pad, Voice, or Mixer configuration.

Local Media Deck has its own independent headphone monitor. Sound Pad and microphone direct monitoring behavior is unchanged.
