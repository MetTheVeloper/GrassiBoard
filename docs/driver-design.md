# Driver design

Milestone 5 builds on the minimal Windows desktop WaveRT driver derived from Microsoft's official SysVAD sample at pinned commit `ef7c3074748ab05726c3a9161d3256118efd76e2`. Only the common miniport implementation, one speaker path, and one microphone path are compiled.

The root device uses hardware ID `ROOT\GrassiBoardVirtualAudio`, service/binary name `GrassiBoardVirtualAudio`, and GrassiBoard-specific product and endpoint-name GUIDs. It exposes exactly two interfaces: render `GrassiBoard Virtual Cable Input` and capture `GrassiBoard Virtual Microphone`.

The system render and system capture streams advertise the same fixed device format: 48 kHz, 16-bit, stereo PCM. Windows Audio converts client formats; kernel mode performs no resampling or DSP.

The render DPC copies consumed WaveRT frames into a preallocated 250 ms SPSC ring. Capture waits for a 10 ms pre-roll, copies available frames into its WaveRT buffer, and zero-fills every unavailable byte. Pause, stop, and restart invalidate queued bytes so a previous session cannot repeat. The ring drops newest frames on overrun, returns silence on underrun, and records fill/underrun/overrun counters. PCM is accepted only from the system-render pin and delivered only to the system-capture pin.

This milestone tests the cable without depending on the GrassiBoard app. DSP remains in user mode, and app-to-cable routing is deferred to Milestone 6.

CI creates a short-lived code-signing certificate, signs the SYS and generated CAT, exports only the public CER, verifies the signatures, and destroys the PFX/private key before upload. Installation requires an explicit administrator-controlled TESTSIGNING/reboot flow. Install and removal scripts never change Secure Boot, BitLocker, or reboot automatically.
