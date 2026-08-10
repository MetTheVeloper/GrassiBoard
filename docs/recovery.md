# Recovery

GrassiBoard installs no kernel driver, Windows service, certificate, or test-signing setting. WASAPI streams are process-owned and are released on normal exit or process termination.

While the engine is live, loss of the selected physical microphone triggers an in-process route rebuild. GrassiBoard excludes known virtual-cable endpoints, selects the next available physical/default capture device, reapplies the existing Voice and Mixer state, and resumes the same virtual output. If no physical microphone is available, the microphone branch is force-muted and recovery is retried without changing the user's persisted Mute setting. Reconnecting a usable microphone permits automatic recovery.

The installed build can be removed through Windows Apps & features or `GrassiBoard.Uninstall.exe`. Uninstall deletes only manifest-owned application files, shortcuts, and the uninstall registration. It deliberately preserves `%APPDATA%\GrassiBoard`, `%LOCALAPPDATA%\GrassiBoard`, original Sound Pad/Media files, and any independently installed virtual cable.
