# Installer

`GrassiBoard.Installer` is the branded .NET 8 WPF bootstrapper used for the v1.0.0 Windows x64 package. CI embeds the verified portable ZIP as a compressed resource and publishes a self-contained single-file `GrassiBoard-Setup` executable.

The installer keeps the supplied 1200×750 poster visible above a compact state area for destination selection, installation progress, completion, and launch. It installs per-user, creates Start Menu and optional Desktop shortcuts, registers an Apps & features uninstall entry, and removes only files recorded in its manifest.

VB-CABLE is never redistributed. Setup checks active audio endpoints immediately before installation; a missing cable does not block the app install and produces an official publisher download link on the completion screen.
