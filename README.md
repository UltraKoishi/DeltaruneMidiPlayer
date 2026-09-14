# Deltarune MIDI Player

**English** | [Русский](README.ru.md)

A Windows application that converts MIDI files into key sequences and plays them in DELTARUNE through AutoHotkey v2.

> This is an independent fan-made utility. The project is not affiliated with the creators or publishers of DELTARUNE.

## Features

- converts `.mid` and `.midi` files into text command sequences;
- stores every active MIDI channel in one sequence file per composition;
- lets you select and play a channel directly in the application;
- supports all MIDI `Set Tempo` events and BPM changes within a composition;
- provides detailed reports about polyphony conflicts and short notes;
- keeps only the highest note in fully synchronized chords;
- plays sequences through AutoHotkey v2 with F9/F10 hotkeys;
- can press a configurable recording hotkey before and after playback (default: `Numpad7`);
- includes English and Russian interface languages;
- includes system, light, and dark themes;
- saves the selected theme, language, window size, and window state;
- supports dragging MIDI files directly onto the application window;
- allows copying and clearing the activity log.

## System requirements

For the prebuilt portable release:

- Windows 10 or Windows 11 x64;
- [AutoHotkey v2](https://www.autohotkey.com/) for sequence playback;
- [Organ Control Expansion](https://gamebanana.com/mods/648107) for full in-game functionality.

The .NET Runtime does not need to be installed because the release build is self-contained.

Building the application from source requires the .NET 10 SDK.

## Usage

1. Place a MIDI file in the `MIDI` directory or drag it onto the application window.
2. Click **Convert MIDI**.
3. One file such as `song.txt` will appear in `NoteSequences`; it contains all active MIDI channels.
4. Select the generated composition, choose the required channel in the **MIDI channel** list, and click **Start playback**.
5. Switch to the game and press F9. Only the selected channel starts. Press F10 to close the AutoHotkey script.

To control OBS recording automatically, open **Settings**, enable **Start and stop recording with playback**, and select the same key configured in OBS. The default is `Numpad7`. The key is pressed before the first note and again after the last note; stopping the AHK script also stops an active recording.

## Simultaneous-note handling

Each MIDI channel is validated and exported as an independent monophonic sequence. Notes on different channels do not create conflicts because only the selected channel is played:

- if several notes start and end at the same time, the highest note is kept and the others are listed in a warning;
- if simultaneously started notes have different durations, or notes overlap while starting at different times, conversion stops;
- the complete MIDI file is validated, so the activity log displays the full numbered list of conflicts;
- exact duplicates of the same note are not treated as separate conflicts.

## Sequence format

```text
[Channel 1]
Instrument|3
C4|100
Sleep|250

[Channel 2]
E4|100
```

## Settings

User preferences are stored in:

```text
%LOCALAPPDATA%\DeltaruneMidiPlayer\settings.json
```

## Building from source

```powershell
dotnet build DeltaruneMidiPlayer.csproj
```

## Creating a release

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\publish.ps1
```

The script creates a self-contained, single-file Windows x64 build and a ZIP archive in the `artifacts` directory.

See [RELEASE.md](RELEASE.md) for additional release information.
