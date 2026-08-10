GrassiBoard v0.11.0 — Profiles, Hotkeys, Tray, Media Deck & Latency

PREREQUISITE: Keep the known working external virtual audio cable installed.

1. Extract the complete portable ZIP and run GrassiBoard.exe.
2. Open Routing, select the physical microphone and the virtual-cable playback/input endpoint.
3. Confirm “Virtual microphone ready,” start the engine, and verify the paired recording endpoint in Voice Recorder or Telegram.
4. Confirm the accepted microphone Pitch/Formant, Mixer, Soundboard, and theme behavior still works.
5. Create and apply a user Voice + Mixer preset, restart, then test update/duplicate/rename/delete and a preset hotkey.
6. Assign a Pad hotkey and test it with another application focused and while GrassiBoard is minimized to Tray.
7. Test Mute, Stop All, Voice FX, Push-to-Talk (hold=open/release=muted), Show/Hide, and conflict reporting.
8. Test Tray Show/Mute/Stop All/Exit, Start Minimized, and optional Start with Windows.
9. Load a long local audio file in Media Deck; test Play/Pause/Resume/Stop, timeline seek, ±10 seconds, volume, and Media hotkeys.
10. Test Monitor/Send as ON/ON, ON/OFF, OFF/ON, OFF/OFF. Speak over Media and confirm only Media—not the microphone—is heard in headphones.
11. While Media plays, trigger Pads and presets; inspect Media fill/underruns and U/O/D diagnostics for crackling or growth.
12. Global Stop All must stop Pads, Media, and the engine without deleting configuration; Start Engine must work afterward.
13. Restart and confirm Profile, Pad, Preset, Hotkey, Media volume, Monitor, and Send settings return without autoplay.

The ZIP does not install a virtual driver. Sound files stay in their original folders. Only Media Deck has the new independent headphone monitor; the microphone is never sent there. If the app encounters an unexpected error, send `%LOCALAPPDATA%\GrassiBoard\CrashReports\latest.txt` with the report.

REPORT
Use the complete report template in docs/test-plan.md.
