# UI architecture

## Application shell

v0.8.x establishes the persistent GrassiBoard shell extended by v0.9.0:

- left sidebar: Board, Voice, Mixer, Routing, Settings;
- top bar: engine health, Mic/Soundboard/Master meters, Mute Mic, Stop All;
- page header and one navigation content region;
- shared dark/mint design tokens and control styles in `Themes` resource dictionaries.

The design target is 1280×800 and the minimum supported layout is 1024×700. Page content scrolls where required. Board uses an adaptive WrapPanel so Pad cards reflow instead of being tied to fixed rows.

## Page ownership

- **Board**: Sound Pad grid, empty/drop state, and compact Voice FX.
- **Voice**: full Pitch, Fine Pitch, Formant, preservation, quality, and DSP latency.
- **Mixer**: live bus gain, voice dynamics, ducking, output protection, Wet/Dry, and presets.
- **Routing**: physical input, virtual output, paired target microphone, refresh, and engine lifecycle.
- **Settings**: copy-safe diagnostics and About/build information.

All pages bind to the same `MainViewModel`. Views do not own or recreate audio services. `MainWindow` code-behind is limited to lifecycle and opening the Pad editor; Board code-behind is limited to file drag/drop.

## Shared semantics

- `Voice FX ON` maps to native Pitch bypass **off**. The user-facing control is intentionally positive.
- Reset Voice sets Pitch, Fine Pitch, and Formant to zero but leaves the Voice FX enabled state unchanged.
- Mute Mic does not stop the engine or Sound Pads.
- Global Stop All stops Pad playback and the audio engine, but does not reset Voice, Mixer, routing, presets, or Board configuration. Start Engine remains available afterward.
- Routing exposes raw cable endpoint names because setup requires them; ordinary Board/Voice language says “virtual microphone.”

## Reusable state and accessibility

Sound Pad cards expose distinct idle, loading, ready, playing/looping, missing/error, hover, pressed, and disabled states through text, border, color, and control state rather than color alone. Standard WPF focus and keyboard behavior is retained for buttons, sliders, checkboxes, selectors, and dialogs.
