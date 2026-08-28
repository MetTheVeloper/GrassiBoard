# GrassiLooper Gate 4 record-sync amendment

> Effective 2026-08-28 by explicit user request during real Gate 4 testing.

This document amends the Gate boundary in `docs/looper-roadmap.md` section 76 for the active v1.4 development branch.

The original roadmap scheduled the first implementation of record alignment for Gate 5. During Gate 4 manual testing, the user explicitly required microphone-recorded child layers to reuse the already-established Local Media synchronization behavior **before Gate 4 acceptance**.

Therefore:

- baseline record-position compensation is now part of **Gate 4 hardening**;
- the existing persisted `MediaSyncOffsetMilliseconds` calibration is shared with Looper recording;
- no separate Looper-only calibration setting is introduced;
- Gate 4 compensation accounts for active microphone source buffering, Pitch/DSP latency, and the real Looper local-monitor path;
- the compensation value is snapshotted at Take start and applied as capture pre-roll removal;
- One Cycle must capture the compensation tail before auto-stop so aligned end-of-cycle audio is not truncated;
- a free first-Master recording is not shifted because no prior project clock exists; its accepted trim workflow remains authoritative.

Gate 5 remains mandatory, but its scope is now **validation and refinement** of this shared alignment foundation: quality-mode changes, measurement diagnostics, edge cases, timing stability, and residual fixed-offset investigation after real click/percussive tests.

If this amendment conflicts with the older wording of roadmap section 76, this amendment plus `docs/looper-development-status.md` governs the active branch until the master roadmap is consolidated.
