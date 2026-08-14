GrassiBoard Hotfix 38b-r2 — Phone Mic guided UI

Fixes:
- Removes the Gate 2 black-on-black primary button styling bug.
- Rebuilds /remote-mic around a guided three-step flow:
  1. Audio Engine
  2. Capture Phone Mic
  3. Route to Program
- Uses GrassiMote Material buttons/status chips for primary actions.
- Clearly distinguishes "captured" from "routed into Program".
- Shows Windows Mic as the safe/default Program source until explicit routing.
- Shows a prominent live banner only when Phone Mic is actually routed.
- Moves dense RTP/jitter/native counters into collapsed Advanced diagnostics.
- Bumps PWA shell cache v20 -> v21.

No changes to:
- ABI 10
- native SPSC ring
- jitter/drift bridge
- WebRTC transport
- Pitch/Formant
- Mixer
- Remote Monitor
- VB-CABLE runtime logic

Apply:
powershell -ExecutionPolicy Bypass -File .\Apply-Hotfix38b-r2-V13PhoneMicGuidedUI.ps1

Build/smoke:
powershell -ExecutionPolicy Bypass -File .\tools\Build-LocalRemoteTest.ps1 -Run -RunSmokeTests


r2 validator fix
----------------
The original 38b payload was valid, but its case-sensitive post-write validator
looked for "Advanced" while the UI intentionally renders the eyebrow as
"ADVANCED". The transaction therefore rolled back.

r2 changes only that validator mismatch. The Guided UI payload itself is
unchanged. Before packaging, every post-write marker was checked directly
against the exact payload, case-sensitively.
