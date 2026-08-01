# SysVAD extraction

The kernel driver is a minimal extraction from Microsoft's official SysVAD sample:

- Repository: `https://github.com/microsoft/Windows-driver-samples`
- Path: `audio/sysvad`
- Commit: `ef7c3074748ab05726c3a9161d3256118efd76e2`
- License: Microsoft Public License; see `THIRD-PARTY-MS-PL.txt`

Only the common WaveRT implementation and the speaker/microphone endpoint paths are
compiled. Bluetooth, USB sideband, HDMI, SPDIF, microphone arrays, APOs and keyword
detector packages are intentionally excluded from this milestone.
