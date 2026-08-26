# GrassiBoard — GrassiLooper v1.4 Master Development Roadmap

> **Project:** GrassiBoard  
> **Feature working name:** GrassiLooper  
> **Recommended target release:** v1.4.x  
> **Baseline:** v1.3.0 Remote Phone Microphone / Full Remote Audio — PERSONAL-STABLE  
> **Desktop:** Windows 10/11 x64, WPF / C#  
> **Native engine:** C++ / WASAPI, 48 kHz float processing  
> **Current native ABI:** 10  
> **Primary product goal:** turn GrassiBoard from a live voice processor/soundboard into a practical loop-production workstation whose stems can continue directly inside FL Studio or another DAW.

---

# 0. Product definition

GrassiLooper is **not a DAW**.

It is a fast vocal/beatbox-oriented loop production environment built on top of the existing GrassiBoard voice engine.

The target workflow is:

```text
Create Loop Project
        ↓
Import a base audio file
OR
record the first loop from a microphone
        ↓
Define one sample-accurate Master Loop
        ↓
Add any number of microphone layers
        ↓
Record with current GrassiBoard Voice FX
        ↓
One Cycle / Loop Replace / Overdub
        ↓
Trim / Mute / Solo / Gain / Pan
        ↓
Preview the complete loop
        ↓
Export aligned stems + mixdown + metadata
        ↓
Drop stems into FL Studio / DAW
```

The key product promise is:

> Put down a loop, use one microphone to create many processed vocal/beatbox layers, then export every layer perfectly aligned for continued production in a DAW.

---

# 1. Preserve the accepted GrassiBoard core

Looper development must not destabilize:

```text
Physical Windows microphone
Remote Phone Microphone
Voice FX
Pitch
Fine Pitch
Formant
Voice presets
Mixer / dynamics
Soundboard
Media Deck
Remote Control
Remote Monitor
VB-CABLE Program output
Profiles
Hotkeys
Tray
Installer/build pipeline
```

No Looper feature may require replacing the accepted VB-CABLE route.

No custom Windows audio driver should be revived.

No large MVVM rewrite should be performed merely to add Looper.

---

# 2. Gate 0 — freeze the v1.3 baseline before Looper work

Before writing Looper production code:

1. update repository status documentation to reflect the user's explicit acceptance of the real Windows + Android v1.3 path;
2. mark v1.3.0 as `USER ACCEPTED / PERSONAL-STABLE`;
3. record the current v1.3 implementation commit;
4. record that the real feature path was tested through local builds;
5. do **not** claim that the final installer itself received manual clean-install verification if it did not;
6. preserve that distinction in documentation;
7. update `docs/current-status.md`;
8. update `docs/remote-development-status.md`;
9. update `CHANGELOG.md` if appropriate.

Suggested status-only commit:

```text
docs(remote): mark v1.3.0 as personal-stable
```

Only after this baseline is frozen should Looper development begin.

---

# 3. Development model

Codex / assistant is responsible for architecture, C#, C++, XAML, native ABI, tests, build scripts, CI, persistence, export, documentation, packaging changes, and bug fixes.

The user is responsible for running local builds, real Windows audio testing, real microphone testing, subjective timing tests, UI/UX review, reporting behavior, and explicit Gate acceptance/rejection.

Never require the user to manually edit source/configuration files when the project can do it itself.

---

# 4. Manual acceptance remains authoritative

A green build is not a passed product Gate.

Each Gate requires:

```text
implementation
→ automated tests
→ local build
→ real use
→ explicit user acceptance
```

If a Gate fails, fix that Gate. Do not silently continue into later Gates.

---

# 5. Authoritative status document

The repository must contain:

```text
docs/looper-development-status.md
```

It tracks baseline, current Gate, implemented work, awaiting test, accepted work, known issues, performance measurements, native ABI, manual test notes, and next permitted Gate.

Update it after every development iteration.

---

# 6. Core Looper architecture

Recommended conceptual architecture:

```text
                 GrassiBoard UI
                      │
                 LooperViewModel
                      │
          ┌───────────┼─────────────┐
          │           │             │
          ▼           ▼             ▼
 Project Store   Waveform       Export
                Analysis        Service
          │
          ▼
      LooperService
          │
          ▼
      Native ABI
          │
 ┌────────┴──────────────────────────────┐
 │           Native Looper Engine       │
 │ Master Loop                          │
 │ Track Buffers                        │
 │ Shared Sample Clock                  │
 │ Transport                            │
 │ Record Head                          │
 │ One Cycle / Replace / Overdub        │
 │ Track Mix                            │
 │ Looper Monitor Tap                   │
 └────────────┬─────────────────────────┘
              │
              ▼
      Local Monitor Worker
              │
              ▼
       Headphones / Speaker
```

Do not put Looper implementation directly into `MainViewModel.cs`.

---

# 7. Recommended managed source structure

```text
src/GrassiBoard.App/
├─ Models/Looper/
│  ├─ LooperProjectModel.cs
│  ├─ LooperTrackModel.cs
│  ├─ LooperMasterModel.cs
│  ├─ VoiceFxSnapshot.cs
│  ├─ WaveformEnvelope.cs
│  └─ LooperEnums.cs
├─ Services/Looper/
│  ├─ LooperService.cs
│  ├─ LooperProjectStore.cs
│  ├─ WaveformAnalysisService.cs
│  ├─ LooperMonitorService.cs
│  ├─ LooperExportService.cs
│  └─ LooperDiagnostics.cs
├─ ViewModels/
│  └─ LooperViewModel.cs
└─ Views/Looper/
   ├─ LooperView.xaml
   ├─ LooperTrackRow.xaml
   ├─ WaveformView.cs
   ├─ SelectedTrackEditor.xaml
   ├─ LiveVoiceFxPanel.xaml
   ├─ TrackInspector.xaml
   └─ LooperTransport.xaml
```

Exact names may change if a cleaner convention is found.

---

# 8. WPF component model

Looper UI must be deliberately componentized.

```text
LooperView
│
├─ ProjectHeader
├─ MasterTrackRow
│  └─ WaveformView
├─ TrackList
│  └─ LooperTrackRow × N
│      └─ WaveformView
├─ SelectedTrackEditor
│  └─ WaveformView
├─ TrackInspector
├─ LiveVoiceFxPanel
└─ Transport
```

Do not copy/paste a separate waveform implementation for Master, tracks, and editor.

---

# 9. WaveformView — reusable primitive

Create one reusable efficient waveform renderer, preferably a WPF Custom Control / FrameworkElement using `DependencyProperty` plus `DrawingContext` / `OnRender`.

Do not create thousands of XAML rectangles.

Conceptual properties:

```text
WaveformData
WaveformBrush / TrackColor
PlayheadPosition
SelectionStart
SelectionEnd
IsSelected
IsEditable
IsRecording
IsArmed
ShowTrimHandles
CompactMode
```

The same renderer must support Master compact row, Track compact row, large selected-track editor, first-recording editor, and imported-master editor.

---

# 10. Waveform analysis is a service, not a UI responsibility

No waveform component should read or decode an audio file.

```text
Audio source
    ↓
WaveformAnalysisService
    ↓
Min/Max envelope buckets
    ↓
WaveformView
```

Analysis happens off the UI thread and outside every realtime audio callback. Cache waveform envelopes when practical.

---

# 11. Track color system

Every Master/Track may have an independent display color. Track color is visual metadata and is passed into reusable waveform/track components. Use a deterministic default palette so newly created layers remain visually distinct.

---

# 12. New Looper page

Add Looper as a first-class desktop workspace:

```text
BOARD
VOICE
MIXER
LOOPER
ROUTING
SETTINGS
```

Entering Looper creates a Looper UI session but does not automatically create a project or alter the Program route.

---

# 13. Empty project UX

A new Looper project begins with:

```text
NEW LOOP PROJECT

[ Import Audio ]
      or
[ Record First Loop ]
```

Both paths ultimately produce one `MASTER LOOP`.

---

# 14. Imported Master path

```text
Import Audio
    ↓
decode/analyze
    ↓
large waveform editor
    ↓
drag START / END handles
    ↓
preview selected region
    ↓
Set As Master Loop
```

Selecting the entire file is valid. Only the selected region becomes the active Master audio buffer.

---

# 15. First microphone recording path

An empty project may begin entirely from microphone input.

Because no Master clock exists yet:

```text
Record First Loop
      ↓
free recording
      ↓
user presses Stop
      ↓
waveform editor
      ↓
trim START / END
      ↓
Set As Master Loop
```

Do not blindly use the exact time between Record and Stop as permanent Master length. The user receives one trim step first.

---

# 16. Master Loop is sample-defined

The authoritative project clock is not BPM. It is:

```text
Sample rate: 48,000 Hz
Master loop frames: N
```

BPM and time signature may exist as optional project metadata. They must not replace the sample-accurate Master Loop frame count. No automatic tempo detection is required for v1.4 MVP.

---

# 17. Master trim semantics

Changing Master trim changes Master Loop start, Master Loop end, Master Loop frame count, and project cycle length. Before child layers exist, Master trim may be edited freely.

---

# 18. Lock Master length after dependent tracks exist

For the MVP, once one or more child tracks exist, the Master frame count becomes locked. Do not silently resize/re-time dependent recordings.

The MVP may require child layers to be removed before redefining Master length. Smarter resizing belongs to a later update.

---

# 19. Track trim semantics are different from Master trim

Child Track trim must **not** change project length.

```text
PROJECT = 8 seconds
Track active audio:
0      2                7      8
|------|████████████████|------|
```

The exported Track is still exactly 8 seconds. Outside its active region, render silence.

```text
Master Trim → changes project loop length
Track Trim  → changes active audible region only
```

Do not shift Track content left when trimming its beginning.

---

# 20. Non-destructive Track trimming

Track trimming should initially be metadata:

```text
ActiveStartFrame
ActiveEndFrame
```

Do not destructively delete the underlying Track buffer. The UI may dim waveform regions outside the active range. Use tiny edge fades if required to prevent clicks.

---

# 21. Seam quality

Master loops must wrap without an audible gap. Use zero-crossing assistance where useful plus a very short seam fade/crossfade where needed. Do not time-stretch the Master automatically.

---

# 22. Shared transport

All Master/Track playback uses one authoritative sample clock.

```text
Master
Track 1
Track 2
Track 3
...
       │
       └─ ONE PLAYHEAD
```

No independent drifting Track clocks.

---

# 23. Transport behavior

Global transport:

```text
PLAY
PAUSE
STOP
RECORD
UNDO
```

- **Play:** starts/resumes Master and all active tracks from current playhead.
- **Pause:** freezes project playhead; resume continues from the same location.
- **Stop:** stops playback and returns playhead exactly to loop frame `0`.
- **Loop wrap:** if transport remains playing, `N - 1 → 0` with no intentional gap.

---

# 24. Recording while transport is stopped

If Master exists and the project is stopped at frame `0`:

```text
Record
→ transport starts
→ recording begins immediately at frame 0
```

There is no need for an extra empty cycle.

---

# 25. Recording while transport is already playing

If Record is pressed in the middle of a cycle, Track State becomes `ARMED` and recording begins at the next Master boundary. Pressing Record again before the boundary cancels the arm.

---

# 26. No live microphone monitoring in MVP

While recording:

```text
Master Loop          audible
Existing Tracks      audible
Live Mic             NOT monitored locally
Live Mic             recorded
```

This intentionally avoids delayed self-monitoring. The MVP is a production looper, not yet a stage-performance monitoring system.

---

# 27. Recorded content becomes audible on later playback

After a recording pass has produced audio, that Track may be heard during subsequent cycles. For Overdub this naturally enables building Kick, Snare, Hi-hat, etc. across passes. The live microphone itself is still not directly monitored.

---

# 28. Three recording behaviors

Each Track has one end-of-loop behavior:

```text
One Cycle
Loop Replace
Overdub
```

Suggested UI wording:

```text
END OF LOOP
● Stop Recording
○ Replace From Start
○ Overdub From Start
```

---

# 29. One Cycle

Record from a boundary for one complete Master cycle and automatically stop recording at the next boundary. Project playback may continue; only recording stops.

---

# 30. Loop Replace

This is circular overwrite.

Required reference case:

```text
Master length = 8 sec
Recording duration = 12 sec
```

Input:

```text
0────────4────────8────────12
[A       ][B       ][C       ]
```

Final 8-second Track:

```text
0────────4────────8
[C       ][B       ]
```

Meaning:

```text
Track 0–4 sec = input 8–12 sec
Track 4–8 sec = input 4–8 sec
```

This behavior must receive a deterministic automated test.

---

# 31. Overdub

Overdub wraps exactly like Replace, but new audio is added to existing Track audio. Initial overdub on an empty Track behaves like normal recording. Later cycles accumulate.

Use float accumulation with safe finite-value validation. Do not repeatedly hard-clip every overdub pass. Playback/output protection belongs to the downstream mix path.

---

# 32. Stop/cancel recording

During active recording, Global Stop safely finalizes the valid portion of the take, stops transport, and returns to frame `0`. A dedicated Cancel/Discard Take action may restore the pre-record Track state. Never leave a partially corrupted Track after interrupted recording.

---

# 33. Undo model

At minimum support one meaningful Undo level for the latest destructive Track operation, including Re-record, Replace, Overdub, and Trim change.

An entire multi-cycle Overdub session counts as one Undo action. Redo is desirable if cheap, but Undo is mandatory.

---

# 34. Looper microphone source

Looper records from whichever GrassiBoard microphone source is actively selected:

```text
Windows physical microphone
OR
Remote Phone Microphone
```

No separate Looper capture driver should be created. If the input source changes unexpectedly during an active Take, fail safely rather than silently combining two microphones into one Track.

---

# 35. Dedicated processed Looper Record Tap

The current Voice pipeline remains the single Voice FX engine.

Conceptual tap:

```text
Selected Mic Source
      ↓
Pitch
Fine Pitch
Formant
Preserve Character
Voice FX
Pitch Dry/Wet
      ↓
[ LOOPER RECORD TAP ]
      ↓
Program Mic Mute
Mic Gain
Gate
Compressor
      ↓
Program Mix
```

Program Mic Mute should not automatically destroy a deliberate local Looper recording. Looper Record has its own explicit Record state.

---

# 36. Expected ABI progression

Current production ABI is 10. A dedicated Looper recording/playback boundary will likely justify ABI 11, but do not bump ABI until native public functions actually change.

Possible conceptual API families:

```text
gb_looper_configure(...)
gb_looper_reset(...)
gb_looper_set_transport(...)
gb_looper_get_state(...)
gb_looper_load_master(...)
gb_looper_load_track(...)
gb_looper_remove_track(...)
gb_looper_set_track_mix(...)
gb_looper_arm_record(...)
gb_looper_stop_record(...)
gb_looper_cancel_record(...)
gb_looper_get_statistics(...)
gb_looper_monitor_read(...)
```

Exact names are implementation details.

---

# 37. Native realtime rules remain absolute

The Looper render/record path may read preallocated buffers, increment playhead, perform bounded modulo/wrap operations, mix fixed buffers, write into preallocated Track buffers, write monitor PCM into an SPSC ring, and update atomic counters/state.

It must not open files, write WAV files, decode MP3, serialize JSON, allocate large buffers, resize collections, access WPF, perform network I/O, perform WebRTC, log, wait on blocking locks, or perform ZIP export.

---

# 38. Looper Track buffers

After Master is defined:

```text
LoopFrames = MasterLoopFrames
```

Every child microphone Track owns an audio buffer aligned to exactly this frame count.

Recommended child-track representation:

```text
48 kHz
mono
float PCM
LoopFrames long
```

Track pan/gain are playback metadata, not baked into recorded mic PCM. Imported Master may remain stereo.

---

# 39. Do not assume infinite memory

The UI/data model must not assume a fixed five-track hardware looper, but production memory must remain bounded.

Benchmark combinations such as:

```text
8 sec / 30 sec / 60 sec / 120 sec loops
8 / 16 / 32 tracks
```

Measure native memory, managed memory, allocation spikes, load/save time, and CPU. Then choose a conservative initial safety ceiling.

---

# 40. Looper local monitor

Master and Track playback must reach the user's headphones/speaker independently from microphone self-monitoring.

Recommended path:

```text
Native Looper Mix
      ↓
bounded Looper Monitor SPSC ring
      ↓
managed LooperMonitorService
      ↓
selected physical render endpoint
```

Reuse accepted managed audio-monitor patterns where practical. Looper monitoring must not depend on VB-CABLE being audible.

---

# 41. Program route isolation

Looper work must not accidentally change the accepted Program microphone route.

For the MVP:

```text
Looper Monitor Mix ≠ Program Mix
```

Whether the finished Looper Mix should optionally be sent into Program/VB-CABLE can be added later. Initial product goal is composition + stem export.

---

# 42. Record alignment compensation

This is a critical Gate.

The performer hears Master/Tracks through the local Looper monitor path. Microphone samples arrive after capture latency + Voice DSP latency + buffer/ring latency. The heard Master also has Looper monitor queue + render-device output latency.

A recorded vocal must be placed on the loop timeline so it aligns with the beat the performer actually heard.

Conceptually:

```text
record write position
=
current Master playhead
-
estimated total capture/DSP/monitor latency
+
user calibration
```

Do not simply stamp processed microphone audio at the current uncorrected render playhead.

---

# 43. Reuse lessons from Media Vocal Sync

The existing Media Deck already solved a related synchronization problem. Looper should reuse compatible latency measurement/calibration concepts rather than invent unrelated timing logic.

Add a Looper-specific signed calibration only if the actual monitor/capture path requires it. Suggested user range: `-100 ms … +100 ms`. Exact defaults must come from real-device testing.

---

# 44. Latency acceptance test

Create a repeatable test with a sharp metronome/click Master and short vocal/percussive clicks recorded against it. Measure transient offset.

Test Voice FX OFF, Pitch active, different quality modes, Windows Mic, and Phone Mic where practical. No growing offset across loop cycles is acceptable.

---

# 45. Voice FX panel

The Looper right sidebar exposes the same active Voice state as the existing Voice page:

```text
LIVE INPUT
Voice FX
Preset
Pitch
Fine Pitch
Formant
Dry / Wet
Preserve Character
Quality / advanced where appropriate
Input meter
```

Do not create a second FX engine or duplicate native DSP parameters.

---

# 46. Reusable Voice controls

Extract reusable Voice UI pieces from the current Voice page where practical.

```text
VoiceView ─┐
           ├→ shared VoiceFxControls → same application state
Looper   ──┘
```

Do not copy/paste the entire existing Voice XAML into Looper.

---

# 47. Looper Voice session scope

Entering Looper:

```text
Current Voice State
→ capture as PreLooperVoiceState
```

Looper temporarily controls the same Voice engine.

Leaving Looper:

```text
restore PreLooperVoiceState
```

This restoration must happen on normal page navigation, project close, Looper exit, and application shutdown where possible.

---

# 48. Looper FX changes should be transient to the normal Profile

Looper experimentation must not unexpectedly overwrite the user's normal Profile Voice settings.

While Looper session scope is active, Voice FX changes affect live engine/UI but should not permanently replace normal Profile defaults. If necessary, temporarily suppress Profile persistence for Looper Voice recalls.

On Looper exit, restore original state and resume normal persistence. A crash should not leave the user's saved normal Voice profile replaced by a random Looper Track preset.

---

# 49. Per-Track Voice FX metadata

Every microphone-recorded Track stores the Voice settings that were active when the successful Take was committed.

Create a dedicated metadata type such as `VoiceFxSnapshot` including at least:

```text
SchemaVersion
VoiceFxEnabled
Pitch
FinePitch
Formant
PreserveVocalCharacter
QualityIndex
PitchWetMix
optional originating preset display name
```

Store actual numeric state. Do not rely only on a preset ID/name because presets may later be renamed or deleted.

---

# 50. Do not include Program mixer state in VoiceFxSnapshot

The Track Voice snapshot should describe **Voice processing**, not the complete GrassiBoard mix.

Do not bake unrelated values such as Soundboard Gain, Master Gain, Ducking, or Limiter into the meaning of a Track's recorded Voice character.

Track gain/pan/mute/solo belong to Looper Track metadata.

---

# 51. Track selection recalls Voice state

Selecting a microphone-recorded Track:

```text
Track selected
→ read VoiceFxSnapshot
→ apply to existing GrassiBoard Voice engine
→ Looper Live Input panel updates
```

Use the application's centralized parameter path. If selected Master came from imported audio and has no Voice snapshot, do not alter Voice settings.

---

# 52. Track metadata changes only on committed audio operations

Changing current Live Voice controls after selecting a Track must not rewrite the historical metadata of already-recorded audio.

Example:

```text
Track recorded with Pitch -7
→ select Track
→ recalls -7
→ user changes Live Input to +3
```

The existing Track is still recorded with `-7`. Its metadata becomes `+3` only after a new committed re-record/Take that actually used `+3`.

---

# 53. Recall as workflow

The Track snapshot enables a fast creative workflow:

```text
Select Bass Track
→ Pitch/Formant recalled
→ Add Layer
→ record another Bass layer
```

This is a deliberate product feature.

---

# 54. Dry-source architecture — future, not MVP

The v1.4 MVP records the processed Voice result:

```text
Mic
→ Voice FX
→ Looper Track
```

Do not require keeping Dry microphone audio in the first release. However avoid architectural decisions that make future Dry storage impossible.

Future model:

```text
Track
├─ dry source
├─ VoiceFxSnapshot
└─ rendered wet audio
```

---

# 55. Non-destructive per-Track Voice FX — future

Do not run an independent Signalsmith Voice pipeline for every active Track in v1.4 MVP. Future versions may render a Dry source through a selected Track FX snapshot offline or with dedicated per-Track processing.

---

# 56. Track row UX

Compact Track representation:

```text
[VOCAL 2]  ~~~~~~~~~ waveform ~~~~~~~~~  [M] [S]  82%
```

Keep rows low-height. Primary purpose is to identify, see waveform, see playing/armed/recording state, select, mute, solo, and see level.

Do not put every editing control into every row.

---

# 57. Selected Track Editor

When a Track is selected, show a larger shared WaveformView with trim handles, larger waveform, Track name, Track volume, Pan, Mute, Solo, recording mode, Re-record, Clear, and Undo where appropriate.

---

# 58. Track Mixer

Each child Track should initially support:

```text
Gain
Pan
Mute
Solo
```

No compressor/EQ/limiter per Track in MVP.

Track Gain/Pan affect preview/mixdown. Raw exported vocal stems should remain available without destructive Gain/Pan baking unless an explicit rendered-stem mode is introduced later.

---

# 59. Solo semantics

If one or more Tracks are soloed, only soloed child Tracks plus Master should be audible. Master remains audible by default because it defines the composition reference.

---

# 60. Project model

Conceptual document:

```text
LooperProject
{
  SchemaVersion
  Id
  Name
  CreatedUtc
  ModifiedUtc
  SampleRate = 48000
  LoopFrames
  OptionalBpm
  OptionalTimeSignature
  Master
  Tracks[]
}
```

---

# 61. Master model

Conceptual fields:

```text
Id
Name
SourceType = Imported | Microphone
Asset
ChannelCount
LoopFrames
Color
Optional VoiceFxSnapshot
WaveformCacheMetadata
```

Imported Master has no Voice snapshot. Microphone-created Master does.

---

# 62. Track model

Conceptual fields:

```text
Id
Name
Asset
Color
RecordingMode
VoiceFxSnapshot
Gain
Pan
Muted
Solo
ActiveStartFrame
ActiveEndFrame
CreatedUtc
ModifiedUtc
```

Do not persist absolute private file paths into portable/export metadata unless truly necessary.

---

# 63. Project storage

Create an internal project workspace such as:

```text
%LOCALAPPDATA%\GrassiBoard\LooperProjects\<project-id>\
```

Conceptual contents:

```text
project.json
assets\
  master.wav
  track-001.wav
  track-002.wav
waveforms\
  ...
```

After a Master/Track has been materialized into project assets, moving the original imported source should not break the project.

---

# 64. Working audio format

Recommended internal committed Track format:

```text
48 kHz
32-bit float WAV
```

Mic Tracks may remain mono. Master may remain stereo. This keeps project assets inspectable and avoids unnecessary quantization during repeated editing.

Disk writing always occurs outside realtime callback.

---

# 65. Autosave

Persist meaningful project changes automatically: Master commit, Track commit, Track delete, Trim change, Gain/Pan, Mute/Solo, Rename, and FX snapshot commit.

Use safe write semantics: temporary file → flush → atomic replace where practical. A crash should not corrupt the entire project document.

---

# 66. Track commit lifecycle

During live recording use a native/in-memory working buffer. After Take finishes: finalize → create/update Track asset asynchronously → persist metadata → regenerate waveform envelope.

Do not block realtime audio while writing the WAV.

---

# 67. Export objective

The core export product is DAW-ready aligned stems.

Example:

```text
MyLoop_92BPM.zip
│
├─ 01_MASTER.wav
├─ 02_BEATBOX.wav
├─ 03_BASS.wav
├─ 04_VERSE.wav
├─ 05_ADLIB.wav
├─ MIXDOWN.wav
└─ project.json
```

---

# 68. Stem alignment contract

Every exported Stem must start at project frame 0, use 48 kHz, contain exactly `LoopFrames`, end at the same sample, loop at the same boundary, and retain silence where Track is inactive.

Do not auto-trim silence, normalize stems automatically, or shift Track content to the beginning.

This contract is more important than file-size optimization.

---

# 69. Stem content

Default export:

- **MASTER:** active Master loop.
- **MIC TRACKS:** post-Voice-FX audio before Looper Track Gain/Pan; active-region trim becomes silence masking.
- **MIXDOWN:** Master + audible Tracks + Track Gain + Pan + Mute state, rendered stereo.

Do not include Solo as a destructive permanent export property unless user explicitly chooses to export the currently soloed mix.

---

# 70. Export metadata

`project.json` should include schemaVersion, project name, sampleRate, loopFrames, duration, optional BPM/time signature, Master metadata, and per-Track name/file/color/active region/recording mode/gain/pan/VoiceFxSnapshot.

Use relative filenames.

---

# 71. Export validation

Automated export tests must open generated WAVs and verify sample rate `48000`, frame count `LoopFrames`, expected channel count, valid headers, expected ZIP files, and valid `project.json` references.

Use exact frame counts, not rounded duration strings.

---

# 72. Gate 1 — UI foundation + project model + waveform

Implement:

```text
Looper navigation page
empty-project screen
project model
project store
WaveformAnalysisService
WaveformView
Track component structure
import audio
large Master trim editor
Set As Master UI model
```

No complicated multi-track recording yet.

Manual acceptance:

```text
Looper page opens
Import works
Waveform looks correct
Track color rendering works
Trim handles feel usable
Master selection is clear
UI structure feels clean
no regression elsewhere
```

---

# 73. Gate 2 — Master Loop engine + transport + monitor

Implement sample-accurate Master buffer, shared Looper playhead, Play, Pause, Stop, gapless wrap, Looper monitor output, playhead visualization, and Master length lock contract.

Run memory/performance benchmark and establish initial supported Loop-size safety limits.

Acceptance:

```text
Master loops cleanly
no audible growing gap
Pause resumes correctly
Stop returns to frame 0
playhead remains stable
30–60 min loop does not drift/grow delay
normal GrassiBoard still works
```

---

# 74. Gate 3 — first microphone Master + dedicated record tap

Implement ABI change if required, processed Voice Looper Record Tap, free first-loop microphone capture, Stop, waveform generation, trim, Set As Master, Windows Mic, Remote Phone Mic compatibility, and Program Mic Mute isolation.

Acceptance:

```text
empty project can become a loop using only mic
FX are printed correctly
Program Mute does not unintentionally erase local recording
Phone Mic can create a Master when routed
Master plays back cleanly
existing Program path remains unchanged
```

---

# 75. Gate 4 — child Track recording engine

Implement Add Layer, Arm at next boundary, One Cycle, Loop Replace, Overdub, Track buffers, record states, record cancel, and basic Undo.

Required deterministic Replace test:

```text
loop = 8 seconds
input = 12 seconds
result first 4 seconds = input 8–12
result final 4 seconds = input 4–8
```

Acceptance: user creates multiple real vocal/beatbox layers and confirms all three modes behave exactly as expected.

---

# 76. Gate 5 — record alignment

Implement/validate capture/DSP latency accounting, Looper monitor latency accounting, record-position compensation, optional signed calibration, and quality-mode changes.

Do not continue until timing is musically usable.

Acceptance using a click/percussive reference:

```text
recorded hits remain aligned
no growing offset
loop wrap remains aligned
Pitch ON/OFF remain usable
```

This Gate is mandatory.

---

# 77. Gate 6 — Voice snapshot session

Implement PreLooperVoiceState, transient Looper Voice session, VoiceFxSnapshot per committed Track, Track-select recall, Profile persistence isolation, restore on Looper exit, and reusable LiveVoiceFxPanel.

Acceptance:

```text
normal Voice state = A
enter Looper
Track 1 record with B
Track 2 record with C
select Track 1 → B recalled
select Track 2 → C recalled
exit Looper → normal Voice state A restored
```

Restart GrassiBoard and confirm normal profile did not become B or C accidentally.

---

# 78. Gate 7 — Track editor + mixer + waveform polish

Implement compact rows, selected editor, non-destructive Track trim, Gain, Pan, Mute, Solo, Track colors, rename, delete, Re-record, and Undo polish.

Acceptance: Track editing feels fast and does not resemble a squeezed desktop DAW timeline.

---

# 79. Gate 8 — persistence + DAW export

Implement project autosave, project reopen, asset recovery/error states, stem render, mixdown, project.json, and ZIP.

Manual DAW acceptance:

1. export one project;
2. extract ZIP;
3. drag all stems into FL Studio;
4. place them all at the same start;
5. confirm they align perfectly;
6. loop the exported region;
7. confirm end/start boundary remains correct.

This is a major product acceptance Gate.

---

# 80. Gate 9 — final regression / personal-stable

Run Windows Mic, Phone Mic, Voice FX, Soundboard, Media, Remote Control, Remote Monitor, VB-CABLE, Profiles, Hotkeys, Tray, and Looper.

Perform a long mixed Looper session and check CPU, memory, Track count, audio underruns, monitor stability, record alignment, project autosave, reopen, and export.

Only then mark:

```text
v1.4.x GrassiLooper MVP
USER ACCEPTED / PERSONAL-STABLE
```

Suggested final commit:

```text
feat(looper): complete v1.4 GrassiLooper production workflow
```

---

# 81. Automated native tests

At minimum:

```text
Master modulo wrap
Play/Pause/Stop playhead behavior
One Cycle exact stop
Replace circular overwrite
12 sec → 8 sec reference behavior
Overdub accumulation
finite-value safety
Track mute
Track solo
Gain/Pan
record cancel restore
Undo restore
Looper monitor ring bounds
record buffer bounds
input-source loss
engine stop during recording
```

No test may require disk access from realtime code.

---

# 82. Automated managed tests

At minimum:

```text
project serialization
schema migration
safe autosave
missing project asset
Waveform envelope generation
Track trim mask
VoiceFxSnapshot clone/restore
PreLooperVoiceState restore
Profile persistence isolation
Track filename sanitization
WAV writing
exact frame-count export
ZIP manifest
project.json paths
```

---

# 83. UI/source smoke tests

Where practical, test Looper page reachability, WaveformView reuse, shared Voice state architecture, StaticResource validity, safe binding modes, Track list virtualization, and avoiding unnecessary heavyweight editors for large Track counts.

---

# 84. Diagnostics

Useful values:

```text
Looper state
LoopFrames
playhead frame
Master duration
active Track count
record state
record mode
record target
record alignment frames
monitor ring fill
monitor underruns/overruns
native track memory
waveform cache status
latest project save
```

---

# 85. Failure isolation

Examples:

```text
Export fails → current playback survives
Project asset missing → other Tracks survive
Looper monitor endpoint disappears → project remains open
Remote Phone Mic disconnects → active Take fails safely
Looper subsystem fails → Board/Voice/Mixer/Program remain usable
```

Never stop the entire audio engine solely because ZIP export failed.

---

# 86. Remote/GrassiMote scope

GrassiMote Looper control is **not required for v1.4 MVP**. Existing Remote must not regress.

Future Remote Looper UI may later expose Play, Stop, Record, Undo, Track selection, Mute/Solo, record mode, and Voice snapshot recall.

---

# 87. Future — Extend / Multiply Master Cycle

Not part of MVP.

Future behavior may support:

```text
Master = 8 sec
Track recording continues 24 sec
→ project expands to 24 sec
→ Master repeats 3 times
```

When introduced, define explicit rules for 1×, 2×, 4×, arbitrary multiples, existing child Tracks, export, undo, and Master redefinition.

---

# 88. Future — Dry source + reprocess

Future Track representation:

```text
Track
├─ source.dry.wav
├─ VoiceFxSnapshot
└─ rendered.wet.wav
```

Possible future features: change Pitch/Formant after recording, apply future Voice FX, A/B FX, re-render offline, and restore original Dry performance.

Design current metadata so this extension remains possible.

---

# 89. Future — per-Track non-destructive FX

Potential later architecture:

```text
Dry Track
→ Track FX
→ playback
```

Do not implement until CPU/performance architecture is separately designed. Offline rendering may be preferable to many concurrent live Voice processors.

---

# 90. Future — live performance mode

The v1.4 MVP intentionally avoids live self-monitoring.

A later Performance mode may investigate ultra-low-latency mic monitoring, foot controls, MIDI, Stream Deck, hardware-style record controls, quantized scene launching, Program output of Looper mix, and remote phone control.

Treat this as a separate product stage.

---

# 91. Future — DAW integration

Stem Export is the first DAW bridge. Do not make v1.4 depend on ASIO/VST.

Future research may evaluate VST3 version of GrassiBoard Voice DSP, ASIO bridge, DAW sync, MIDI clock, Ableton Link, and direct project interchange. Each is a separate architecture decision.

---

# 92. Future — additional Looper sources

MVP child layers are microphone-driven.

Future Track sources may include Soundboard Pad, Media Deck, system audio, imported one-shot, imported loop, and Remote audio.

Do not expand the initial Track recorder into a generic DAW routing matrix.

---

# 93. No scope creep

v1.4 does **not** require:

```text
piano roll
MIDI sequencing
automation lanes
plugins
EQ per Track
arrangement timeline
time stretching
tempo warping
pitch correction
spectral editing
multi-take comping
full DAW mixer
cloud projects
collaboration
```

The goal is a great Looper, not FL Studio inside GrassiBoard.

---

# 94. Final MVP UX target

```text
┌──────────────────────────────────────────────────────────────┐
│ LOOP PROJECT                          BPM optional   4/4     │
├───────────────────────────────────────────┬──────────────────┤
│ MASTER                                    │ LIVE INPUT       │
│ ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~  │                  │
│                                           │ Voice FX   ON    │
│ VOCAL 1  ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~  │ Preset           │
│ VOCAL 2  ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~  │ Pitch            │
│ BEATBOX  ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~  │ Fine             │
│ ADLIB    ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~  │ Formant          │
│                                           │ Dry / Wet        │
│ + ADD LAYER                               │ Preserve         │
│                                           │ Input meter      │
├───────────────────────────────────────────┤                  │
│ SELECTED TRACK — VOCAL 2                  │                  │
│ |<------ editable waveform ------------>| │                  │
│ Gain   Pan   Mute   Solo                  │                  │
│ End: One Cycle / Replace / Overdub        │                  │
├───────────────────────────────────────────┴──────────────────┤
│  UNDO        ■ STOP      ❚❚ PAUSE      ▶ PLAY      ● REC   │
└──────────────────────────────────────────────────────────────┘
```

---

# 95. Final MVP success scenario

The second monster is successful when:

1. User opens GrassiBoard and Looper.
2. Creates a blank project.
3. Records a beatbox pattern as first loop.
4. Stops and trims it precisely.
5. Sets it as Master.
6. Master loops continuously.
7. User hears Master, not live microphone.
8. User sets Pitch/Formant and adds Bass Track.
9. Record is armed and starts exactly on next boundary.
10. Bass Track records one complete cycle.
11. User selects another FX state and adds another Track.
12. Uses Overdub over several cycles.
13. Uses Loop Replace on another Track.
14. Selecting Bass Track recalls original Voice FX state.
15. Child Track trim does not change project length.
16. Mute/Solo/levels work.
17. Stop/restart returns to frame zero.
18. Project closes/reopens without losing work.
19. ZIP export succeeds.
20. All stems align in FL Studio.
21. Entire exported region loops correctly.
22. Exiting Looper restores the pre-Looper Voice state.
23. Physical Mic still works.
24. Remote Phone Mic still works.
25. Remote Control/Monitor still work.
26. VB-CABLE Program route remains unchanged.

---

# 96. Development Gate summary

```text
v1.3 PERSONAL-STABLE
        │
        ▼
GATE 0
Freeze/document accepted baseline
        │
        ▼
GATE 1
Looper UI + project + reusable waveform architecture
        │
        ▼
GATE 2
Master Loop + transport + local monitor
        │
        ▼
GATE 3
First Mic Master + processed Record Tap / ABI
        │
        ▼
GATE 4
Layer recorder
One Cycle / Replace / Overdub
        │
        ▼
GATE 5
Sample/timing compensation
        │
        ▼
GATE 6
Voice FX snapshots + Looper session restore
        │
        ▼
GATE 7
Track editor + mixer + waveform UX
        │
        ▼
GATE 8
Project persistence + DAW-ready ZIP stems
        │
        ▼
GATE 9
Regression + soak + USER ACCEPTANCE
        │
        ▼
v1.4.x GRASSILOOPER PERSONAL-STABLE
```

---

# 97. Mandatory handoff behavior for every new Looper session

At the start of every future Looper-development task:

1. read `docs/looper-development-status.md`;
2. read the relevant Gate in this roadmap;
3. read `docs/current-status.md`;
4. inspect current native ABI;
5. inspect current Looper source before editing;
6. identify the currently permitted Gate;
7. implement only that Gate;
8. update automated tests;
9. run local build/tests;
10. update status documentation;
11. provide the user with a short real-device/manual checklist;
12. wait for explicit test result;
13. patch the same Gate if rejected;
14. only continue after explicit acceptance.

Repository status wins over old chat assumptions if they disagree.

---

# 98. Final principle

Do not build a DAW.

Do not rewrite the stable app.

Do not let waveform UI leak into realtime audio code.

Do not let disk/export logic touch the audio callback.

Do not create a second Voice engine.

Do not sacrifice sample alignment for visual convenience.

Build one thing extremely well:

> **A fast, tactile, sample-accurate vocal loop workstation powered by the Voice engine GrassiBoard already has.**

The first monster made the microphone programmable.

The second monster makes the microphone compositional.
