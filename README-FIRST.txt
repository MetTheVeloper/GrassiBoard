GrassiBoard v1.0.1 — Stable Release

PREREQUISITE: Keep the known working external virtual audio cable installed.

1. Run the branded Setup EXE, or extract the complete portable ZIP and run GrassiBoard.exe.
2. Open Routing, select the physical microphone and the virtual-cable playback/input endpoint.
3. Confirm “Virtual microphone ready,” start the engine, and verify the paired recording endpoint in Voice Recorder or Telegram.
4. Confirm the accepted microphone Pitch/Formant, Mixer, Soundboard, and theme behavior still works.
5. Create and apply a user Voice + Mixer preset, restart, then test update/duplicate/rename/delete and a preset hotkey.
6. Assign a Pad hotkey and test it with another application focused and while GrassiBoard is minimized to Tray.
7. Test Mute, Stop All, Voice FX, Push-to-Talk (hold=open/release=muted), Show/Hide, and conflict reporting.
8. Test Tray Show/Mute/Stop All/Exit, Start Minimized, and optional Start with Windows.
9. Load a long local audio file in Media Deck; test Play/Pause/Resume/Stop, timeline seek while both paused and playing, ±10 seconds, volume, and Media hotkeys.
10. Test Monitor/Send as ON/ON, ON/OFF, OFF/ON, OFF/OFF. Speak over Media and confirm only Media—not the microphone—is heard in headphones.
11. While Media plays, sing or speak on the beat in High quality, Balanced, and Low latency. Confirm the recorded virtual microphone stays aligned with the monitored beat. Settings must report the current Media Vocal Sync value.
    If a device-specific difference remains, adjust Media Sync Calibration live: negative advances Media and positive delays Media. Start around -5 ms when voice is slightly early.
12. Global Stop All must stop Pads, Media, and the engine without deleting configuration; Start Engine must work afterward.
13. While running, disconnect the selected physical microphone. Confirm GrassiBoard switches to the next real microphone without resetting Voice/Mixer/Pad settings. With every physical microphone disconnected, confirm the virtual microphone is safely muted and the app stays alive; reconnect one and confirm automatic recovery.
14. Restart and confirm Profile, Pad, Preset, Hotkey, Media volume, Monitor, and Send settings return without autoplay.
15. Test uninstall from Windows Apps & features. User audio files and `%APPDATA%`/`%LOCALAPPDATA%` settings must not be deleted.

Neither package installs a virtual driver. When no compatible cable is detected, Setup finishes normally and shows the official VB-CABLE download link. Sound files stay in their original folders. If the app encounters an unexpected error, send `%LOCALAPPDATA%\GrassiBoard\CrashReports\latest.txt` with the report.

REPORT
Use the complete report template in docs/test-plan.md.
