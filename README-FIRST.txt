GrassiBoard v0.11.2 — Startup & Application Icon Hotfix

PREREQUISITE: Keep the known working external virtual audio cable installed.

1. Extract the complete portable ZIP and run GrassiBoard.exe.
2. Open Routing, select the physical microphone and the virtual-cable playback/input endpoint.
3. Confirm “Virtual microphone ready,” start the engine, and verify the paired recording endpoint in Voice Recorder or Telegram.
4. Confirm the accepted microphone Pitch/Formant, Mixer, Soundboard, and theme behavior still works.
5. Create and apply a user Voice + Mixer preset, restart, then test update/duplicate/rename/delete and a preset hotkey.
6. Assign a Pad hotkey and test it with another application focused and while GrassiBoard is minimized to Tray.
7. Test Mute, Stop All, Voice FX, Push-to-Talk (hold=open/release=muted), Show/Hide, and conflict reporting.
8. Test Tray Show/Mute/Stop All/Exit, Start Minimized, and optional Start with Windows.
9. Load a long local audio file in Media Deck; test Play/Pause/Resume/Stop, timeline seek while both paused and playing, ±10 seconds, volume, and Media hotkeys.
10. Test Monitor/Send as ON/ON, ON/OFF, OFF/ON, OFF/OFF. Speak over Media and confirm only Media—not the microphone—is heard in headphones.
11. While Media plays, sing or speak on the beat in High quality, Balanced, and Low latency. Confirm the virtual-mic recording stays aligned at 150.0, 53.3, and 26.7 ms respectively while headphone Media monitoring remains direct.
12. Global Stop All must stop Pads, Media, and the engine without deleting configuration; Start Engine must work afterward.
13. Restart and confirm Profile, Pad, Preset, Hotkey, Media volume, Monitor, and Send settings return without autoplay.

The ZIP does not install a virtual driver. Sound files stay in their original folders. Only Media Deck has the new independent headphone monitor; the microphone is never sent there. If the app encounters an unexpected error, send `%LOCALAPPDATA%\GrassiBoard\CrashReports\latest.txt` with the report.

REPORT
Use the complete report template in docs/test-plan.md.
