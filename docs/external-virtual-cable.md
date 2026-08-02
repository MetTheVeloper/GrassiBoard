# External virtual cable setup

## What the cable does

An external cable exposes two Windows audio endpoints:

```text
GrassiBoard renders processed audio
    -> cable playback endpoint (for example, CABLE Input)
    -> cable driver
    -> cable recording endpoint (for example, CABLE Output)
    -> Voice Recorder / OBS / Discord / browser
```

GrassiBoard opens only the physical microphone and the cable playback endpoint. The target application opens the paired recording endpoint.

## Recommended reference

[VB-CABLE](https://vb-audio.com/Cable/) is the reference test dependency. Its publisher describes it as a Windows virtual audio driver where audio sent to the cable input is forwarded to the cable output. It is donationware with separate licensing; GrassiBoard does not download, bundle, modify, or silently install it.

Download only from the publisher, extract the complete archive, run the x64 setup as Administrator, and reboot after installation as instructed by the publisher. Other external cables can work if they expose an active playback/recording pair.

## GrassiBoard configuration

1. Remove the retired GrassiBoard test driver if it is still installed. Disable TESTSIGNING and reboot after confirming its endpoints are gone.
2. Select the real physical microphone under `INPUT MICROPHONE`.
3. Select the cable playback endpoint under `SEND TO VIRTUAL CABLE INPUT`.
4. GrassiBoard uses the Windows Container ID, then a conservative name fallback, to find the paired recording endpoint.
5. Select the endpoint named by GrassiBoard as the microphone in the target application.

If the installed `AMM Virtual Audio Device` exposes a working paired playback/recording endpoint, it can be tested before installing another cable. GrassiBoard is not tied to VB-CABLE.

## Current limitation

`v0.7.0` sends one processed stream to the cable. Separate low-latency headphone monitoring is intentionally deferred so system-wide microphone routing can be accepted independently. Monitoring supplied by a target application may be used for testing, but can add latency.
