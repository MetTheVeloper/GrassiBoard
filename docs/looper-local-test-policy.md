# GrassiLooper local-first development and test policy

> Effective 2026-08-27 for `feature/grassilooper-v1.4`.
> This file is the operational Source of Truth for **how Gate validation is executed**. Where an older document can be read as requiring GitHub-hosted CI during iterative Looper development, this policy overrides that interpretation.

## Core rule

GrassiLooper Gate development is **local-first**.

The assistant implements and commits source changes to `feature/grassilooper-v1.4`. The user pulls the branch and runs the exact local build/test command supplied for that iteration. The user's real Windows/audio result and explicit Gate acceptance/rejection are authoritative.

GitHub Actions is **not** a prerequisite for implementing, handing off, testing, accepting, or rejecting any GrassiLooper Gate.

## Required Gate flow

```text
assistant implements + commits
        ↓
user git pull --ff-only
        ↓
local automated build/tests on user's Windows machine
        ↓
real functional/audio/UI test on user's machine
        ↓
user reports PASS/FAIL
        ↓
assistant fixes same Gate if needed
        ↓
explicit user acceptance
        ↓
next Gate unlocks
```

No Gate may be delayed merely because a GitHub Actions runner is queued, slow, unavailable, or reports a CI-only issue that the local authoritative test path can validate directly.

## Responsibilities

Assistant:
- architecture, C#, C++, XAML, ABI, tests, scripts, documentation, persistence/export and bug fixes;
- commit complete testable iterations to the feature branch;
- provide the exact local automated-test command and the exact manual test checklist;
- interpret the user's output and fix failures;
- never ask the user to manually edit source/config files.

User:
- `git pull` the committed iteration;
- run the supplied local build/automated test command;
- send the resulting PASS/FAIL output when requested;
- perform real Windows microphone/audio/UI tests;
- explicitly accept or reject the Gate.

## GitHub Actions policy during Gate development

- Do **not** keep a pull request open from `feature/grassilooper-v1.4` merely to obtain CI during iterative Gates.
- Do **not** wait for GitHub Actions before handing a committed iteration to the user for local testing.
- Automated smoke/native tests that can run on the user's Windows development machine must be run locally.
- GitHub Actions may be used only when the user explicitly requests it, or for a final integration/release checkpoint after the gated feature work is accepted.
- Release/tag workflows remain release infrastructure and are not part of iterative Gate acceptance.

## Current repository trigger note

The repository `Build` workflow has a `pull_request` trigger. The long-lived draft PR previously used for GrassiLooper was therefore causing a full hosted build after every branch update. That PR is intentionally closed during Gate development. The `Build` workflow does not have a normal push trigger for `feature/grassilooper-v1.4`, so ordinary commits to this branch no longer start that hosted build.

## What counts as automated validation

Automated validation does not mean GitHub Actions. It means deterministic build/test commands, including as applicable:

- native CMake configure/build;
- native `ctest` suite;
- managed compile/smoke checks;
- Nuxt static generation when Remote Web is in the dependency graph;
- local WPF publish/build;
- Gate-specific source/ABI deterministic checks.

These should be wrapped in project scripts whenever practical so the user runs one command instead of manually reproducing CI steps.

## Manual acceptance remains mandatory

Automated tests cannot validate subjective or device-specific audio behavior. Each Gate still requires the relevant real-device checks, such as microphone source, Voice FX, transport timing, monitor routing, seam quality, Phone Mic, VB-CABLE isolation, UI responsiveness, and regression paths.

A Gate passes only after the user explicitly says it passes.