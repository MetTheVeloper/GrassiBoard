# Driver design

Milestone 3 still contains an MSBuild utility placeholder, not a Windows driver. CI explicitly rejects `.inf`, `.cat`, `.sys`, certificate, and PFX files inside the placeholder source directory.

The first driver skeleton is deferred to Milestone 4. It will be derived from the official SysVAD/WaveRT design, use stable project-specific identifiers, and keep every DSP operation in user mode.
