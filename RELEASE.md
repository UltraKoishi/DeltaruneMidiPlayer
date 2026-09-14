# Release guide

## Prerequisites

- Windows 10/11 x64
- .NET 10 SDK
- PowerShell 5.1 or newer

AutoHotkey v2 is required by end users for playback, but is not bundled with the application.

## Build the release package

From the project directory:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\publish.ps1
```

Optional version override:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\publish.ps1 -Version 1.1
```

Use `-NoRestore` only when the required `win-x64` runtime packs have already been restored locally.

Outputs:

```text
artifacts\DeltaruneMidiPlayer-v1.1-win-x64\
artifacts\DeltaruneMidiPlayer-v1.1-win-x64.zip
```

The package is self-contained and includes:

- `DeltaruneMidiPlayer.exe`;
- `Ahk\PlayerTemplate.ahk`;
- empty `MIDI` and `NoteSequences` folders;
- `README.md` and `RELEASE.md`.

## Release checklist

- [ ] Run the application on a clean Windows 10/11 x64 machine.
- [ ] Check Russian and English localization.
- [ ] Check system, light, and dark themes.
- [ ] Convert a MIDI with tempo changes.
- [ ] Convert a MIDI with several simultaneous-note warnings.
- [ ] Convert a multi-channel MIDI and verify that one sequence file contains every active channel.
- [ ] Select and play each MIDI channel from the channel list in the application.
- [ ] Start and stop playback with AutoHotkey v2 using F9/F10.
- [ ] Enable recording control and verify that the selected hotkey starts and stops OBS recording.
- [ ] Verify drag-and-drop import.
- [ ] Scan the ZIP archive with Windows Security.
- [ ] Add a code-signing signature if a certificate is available.
- [ ] Choose and add a software license before publishing the source publicly.

Code signing and the final software-license choice require owner-specific credentials/decisions and are intentionally not automated.
