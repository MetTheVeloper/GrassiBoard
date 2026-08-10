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

[VB-CABLE](https://vb-audio.com/Cable/) is the reference test dependency. Its publisher describes it as a Windows virtual audio driver where audio sent to the cable input is forwarded to the cable output. It is donationware with separate licensing; GrassiBoard does not bundle, modify, or silently install it. When no compatible cable is detected, the v1 installer completes normally and shows the publisher's current [direct Windows package](https://download.vb-audio.com/Download_CABLE/VBCABLE_Driver_Pack45.zip).

Download only from the publisher, extract the complete archive, run the x64 setup as Administrator, and reboot after installation as instructed by the publisher. Other external cables can work if they expose an active playback/recording pair.

## GrassiBoard configuration

1. Remove the retired GrassiBoard test driver if it is still installed. Disable TESTSIGNING and reboot after confirming its endpoints are gone.
2. Select the real physical microphone under `INPUT MICROPHONE`.
3. Select the cable playback endpoint under `SEND TO VIRTUAL CABLE INPUT`.
4. GrassiBoard uses the Windows Container ID, then a conservative name fallback, to find the paired recording endpoint.
5. Select the endpoint named by GrassiBoard as the microphone in the target application.

If the installed `AMM Virtual Audio Device` exposes a working paired playback/recording endpoint, it can be tested before installing another cable. GrassiBoard is not tied to VB-CABLE.

## Product boundary

The cable remains an independent prerequisite with its own installer, reboot requirements, and license. GrassiBoard's installer only detects active endpoints and offers the official link; it never treats a missing cable as an installation failure.
