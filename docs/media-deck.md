# Local Media Deck

Media Deck is a long-form streaming source on Board and is intentionally separate from cached Sound Pads. It supports local WAV, MP3, AIFF and Media Foundation-supported FLAC/AAC/M4A/WMA/MP4/MOV audio tracks. Container support depends on codecs available in the target Windows installation; video is not rendered.

The worker opens the file outside the UI/audio callback, converts to stereo 48 kHz float, and feeds bounded read-ahead. Virtual-mic send writes 20 ms blocks into a preallocated four-second native SPSC ring with a 200 ms target fill. Headphone monitor uses a separate shared-mode WASAPI output and receives Media only. These independent toggles support Monitor/Send ON/ON, ON/OFF, OFF/ON, and OFF/OFF without ever monitoring the user's microphone.

Transport includes load, play/pause/resume, stop-to-zero, direct timeline seek, ±10 seconds, duration/current time, independent volume, and activity/fill meters. Media transport hotkeys use the same commands. Stop All stops Media, Pads, and the engine. Volume/toggles/hotkeys and the last file reference persist, but Media never autoplays. A missing file becomes a visible repair/clear state rather than a crash.

Pitch/Formant and microphone-only dynamics do not affect Media. Media joins the stereo Master bus and therefore receives Master gain, linked limiting, clipping protection, and final safety clamping.
