# Changelog

## 1.1 — 2026-09-06

### Interface

- Moved playback, MIDI conversion, and recording options into a separate settings window.
- Disabled automatic instrument changes by default and enabled simultaneous-note warnings by default.

### MIDI channels

- Added one sequence container per composition with sections for every active MIDI channel.
- Added channel selection in the application with instrument and note-count details.
- Kept playback compatibility with legacy single-channel `_chNN.txt` files.
- Applied monophony validation and Program Change filtering separately to each channel.
- Added reliable low-latency entry for multi-digit instrument numbers without advancing the playback timeline.
- Removed repeated Program Change commands during conversion and ignored them during playback for existing sequences.
- Expanded the supported note range to C0–C10 (MIDI notes 0–120).
- Added optional start/stop recording hotkey automation with a configurable key and `Numpad7` default.

## 1.0.1 — 2026-08-14

### MIDI conversion

- Corrected the supported FL Studio note range from C3–C9 to C2–C8 (MIDI notes 24–96).

## 1.0.0 — 2026-08-09

### Interface

- Added system, light, and dark themes with a native dark title bar.
- Added persistent language, theme, window size, and maximized-state settings.
- Reworked the main window with responsive cards, custom buttons, consistent spacing, and release branding.
- Added activity-log copy and clear actions.
- Added a localized About window.
- Fixed clipping in the expanded settings panel.
- Numbered all warnings and kept the log positioned at its first entry after conversion.

### MIDI conversion

- Reads every MIDI `Set Tempo` meta event and builds an absolute tempo map.
- Calculates note and rest durations across tempo changes.
- Collapses exact duplicate notes.
- Reduces fully synchronized chords to their highest note and reports every skipped note.
- Scans the complete MIDI file and reports all unsupported polyphony conflicts together.
- Preserves leading silence and uses rounded absolute timeline positions to reduce timing drift.

### Release

- Added application metadata, per-monitor DPI support, a multi-resolution icon, and Windows x64 single-file publishing.
- Added a repeatable release script, user documentation, and a release checklist.
