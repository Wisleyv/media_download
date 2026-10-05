# Release packaging

Commit and verify the intended changes before packaging. Run from the repository root:

```powershell
powershell.exe -NoProfile -File scripts/package-release.ps1 -Version v2.0.1
```

The script archives an explicit list of committed application files and documentation from HEAD. Downloaded dependencies (including yt-dlp.exe), settings, logs and older packages are excluded. It refuses to overwrite an existing package or package uncommitted tracked changes.

Create the version tag at that same commit, then publish the generated ZIP and its SHA-256 checksum in the GitHub release. Verify the remote tag commit and download the published asset to compare its checksum before marking the release complete. Never reuse an existing version tag to publish different source code.

## v2.0.1 correction

The historical v2.0.0 tag points to a commit predating the CataMedia application, although its separately uploaded ZIP contains CataMedia. v2.0.1 supersedes that release with a tag and package built from the same source commit. Application behavior and the existing official yt-dlp download/update mechanism are preserved.

The v2.0.0 launcher differences from the working tree were LF versus CRLF line endings only, not functional changes.
