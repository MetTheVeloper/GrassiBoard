# Driver design

Milestone 4 contains a minimal WaveRT skeleton derived from Microsoft's official SysVAD sample at pinned commit `ef7c3074748ab05726c3a9161d3256118efd76e2`. Only the common miniport implementation, one speaker path, and one microphone path are compiled.

The root device uses hardware ID `ROOT\GrassiBoardVirtualAudio`, service/binary name `GrassiBoardVirtualAudio`, and GrassiBoard-specific product and endpoint-name GUIDs. It exposes exactly two interfaces: render `GrassiBoard Virtual Cable Input` and capture `GrassiBoard Virtual Microphone`.

The two endpoints are deliberately independent in this milestone. Render-to-capture PCM transport begins in Milestone 5. DSP remains in user mode.

CI creates a short-lived code-signing certificate, signs the SYS and generated CAT, exports only the public CER, verifies the signatures, and destroys the PFX/private key before upload. Installation requires an explicit administrator-controlled TESTSIGNING/reboot flow. Install and removal scripts never change Secure Boot, BitLocker, or reboot automatically.
