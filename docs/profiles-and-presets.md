# Profiles and user presets

## Profiles

Profiles are complete GrassiBoard working setups. They persist selected input, external-cable output, Media monitor device, Voice/Mixer state, Sound Pads and their hotkeys, user presets and their hotkeys, global actions, and tray/startup/Media preferences in `%APPDATA%\GrassiBoard\profiles.json`.

Create, duplicate, rename, select/apply, and delete are available in Settings. Switching a Profile stops active playback/engine safely, restores the selected configuration, reloads Pad references, and does not autoplay remembered Media. Device IDs are preferred when still present; missing devices fall back safely and never make JSON loading fatal.

The schema is versioned. Loading is fault-isolated: one malformed Profile, Pad, or user preset is skipped without discarding valid siblings. Saves use a temporary file followed by atomic replacement. A legacy `soundboard.json` becomes the first Default Profile.

## Voice + Mixer presets

Built-in Clean, Broadcast, Streaming, and Voice Chat presets remain read-only. User presets capture the complete current Voice character and every user-adjustable Mixer value. Users can Save As, apply, update, duplicate, rename, delete, and optionally assign a global hotkey.

Apply interpolates continuous parameters in 12 steps over approximately 200 ms. Effects required by the target are enabled before the ramp and effects disabled by the target are switched off after it. The transition does not restart WASAPI, change routing, stop Pads/Media, or block the UI. A newer request cancels and supersedes an older transition safely.
