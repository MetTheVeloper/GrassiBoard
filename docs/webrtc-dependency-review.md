# GrassiBoard v1.2 — WebRTC dependency license review

**Status:** Review complete; explicit user distribution/license decision required before production promotion.

## Tested dependency

- Package: `SIPSorcery`
- Tested version: `10.0.13`
- Role: WebRTC / ICE / SDP / RTP transport and managed Opus integration
- Real-device status: accepted for v1.2 Remote Monitor technology gates on Windows + Android

The engineering preference is to avoid changing this package/version before the v1.2 release candidate because the current transport has already passed the full real-device audio matrix.

## Current upstream license finding

The current upstream license is BSD-3-Clause plus an additional geographic restriction. The upstream text says that outside the restricted geography, BSD-3-Clause applies without an extra commercial-use restriction or share-alike requirement.

This is therefore not treated as an ordinary unrestricted BSD dependency for a globally distributed build.

## Why not downgrade to v8.0.12?

The v8.0.12 license file has the classic BSD-3-Clause grant, but it also documents unresolved provenance around a small part of the DTLS/SRTP implementation. It explicitly says the safest downstream course is to assume an AGPL-3.0 claim or remove the affected files; removal would make DTLS/SRTP unusable.

Because WebRTC depends on DTLS/SRTP, v8.0.12 is rejected as a production-license workaround.

## Required user decision

Choose one:

### A — Keep tested SIPSorcery 10.0.13

Use this when the intended GrassiBoard distribution scope is compatible with the upstream geographic restriction.

Next implementation:
- add upstream notice;
- promote ABI 9 and Remote Monitor out of spike-only flags;
- create v1.2 release-candidate local build;
- wire release CI / installer / portable checks;
- run 30–60 minute soak;
- only then mark v1.2 USER ACCEPTED.

### B — Replace transport

Use this if the geographic restriction is unacceptable for the intended distribution model.

Next implementation:
- preserve Windows loopback, Soundboard tap, Media duplicate prevention, My Voice tap and Monitor Mix;
- introduce a transport abstraction around offer/answer/ICE/audio-send;
- replace SIPSorcery;
- repeat transport interoperability, quality, reconnect, packaging and long-session validation.

## Important

This document records an engineering/license gate and is not legal advice.
