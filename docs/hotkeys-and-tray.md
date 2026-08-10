# Global hotkeys and System Tray

Global hotkeys use Windows `RegisterHotKey`; duplicate GrassiBoard assignments, invalid gestures, and registrations rejected by Windows are shown in Settings. Assignments persist per Profile and remain active while another app has focus or GrassiBoard is hidden in the Tray.

Supported actions are Sound Pads, user presets, Mic Mute, Stop All, Voice FX, Show/Hide, Media Play/Pause, Media Stop, Media -10 seconds, and Media +10 seconds. Push-to-Talk uses a low-level keyboard hook only for its configured gesture: hold opens the existing microphone mute path; release mutes it. Removing PTT returns to normal unmuted mode. Hotkeys do not restart the audio engine.

Minimize to Tray hides the main window while the message window and hotkeys stay alive. The Tray menu offers Show, Mute/Unmute Microphone, Stop All, and Exit. Start Minimized is a normal preference. Optional Start with Windows writes a reversible `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` value for the current executable with `--minimized`; no elevation is required.
