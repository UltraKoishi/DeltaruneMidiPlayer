#Requires AutoHotkey v2.0
#SingleInstance Force

global Language := A_Args.Length >= 3 ? A_Args[3] : "ru"

if (A_Args.Length < 1) {
    message := Language = "en" ? "Sequence file path was not provided." : "Не передан путь к файлу последовательности."
    MsgBox(message, "DELTARUNE Piano", "Iconx")
    ExitApp()
}

global SequenceFile := A_Args[1]
global ChangeInstruments := A_Args.Length < 2 || A_Args[2] != "0"
global NoteDelay := A_Args.Length >= 4 ? Max(0, A_Args[4] + 0) : 50
global ControlRecording := A_Args.Length >= 5 && A_Args[5] = "1"
global RecordingHotkey := A_Args.Length >= 6 ? A_Args[6] : "Numpad7"
global StopRequestFile := A_Args.Length >= 7 ? A_Args[7] : ""
global SelectedChannel := A_Args.Length >= 8 ? Max(0, A_Args[8] + 0) : 0
global RecordingKeyHoldMs := 100
global InstrumentKeyHoldMs := 10
global InstrumentKeyGapMs := 10
global LastKeyTime := 0
global DebounceDelay := 50
global IsPlaying := false
global RecordingActive := false
global ActiveKeys := []
global LastInstrumentNumber := ""

global Notes := Map(

    "C0", ["c", "s", "z"],
    "Db0", ["right", "c", "s", "v", "z"],
    "D0", ["right", "c", "s", "z"],
    "Eb0", ["right", "down", "c", "s", "v", "z"],
    "E0", ["right", "down", "c", "s", "z"],
    "F0", ["down", "c", "s", "z"],
    "Gb0", ["left", "down", "c", "s", "v", "z"],
    "G0", ["left", "down", "c", "s", "z"],
    "Ab0", ["left", "c", "s", "v", "z"],
    "A0", ["left", "c", "s", "z"],
    "Bb0", ["left", "up", "c", "s", "v", "z"],
    "B0", ["left", "up", "c", "s", "z"],
    "C1", ["s", "z"],
    "Db1", ["right", "s", "v", "z"],
    "D1", ["right", "s", "z"],
    "Eb1", ["right", "down", "s", "v", "z"],
    "E1", ["right", "down", "s", "z"],
    "F1", ["down", "s", "z"],
    "Gb1", ["left", "down", "s", "v", "z"],
    "G1", ["left", "down", "s", "z"],
    "Ab1", ["left", "s", "v", "z"],
    "A1", ["left", "s", "z"],
    "Bb1", ["left", "up", "s", "v", "z"],
    "B1", ["left", "up", "s", "z"],

    "C2", ["c", "a", "z"],
    "Db2", ["right", "c", "a", "v", "z"],
    "D2", ["right", "c", "a", "z"],
    "Eb2", ["right", "down", "c", "a", "v", "z"],
    "E2", ["right", "down", "c", "a", "z"],
    "F2", ["down", "c", "a", "z"],
    "Gb2", ["left", "down", "c", "a", "v", "z"],
    "G2", ["left", "down", "c", "a", "z"],
    "Ab2", ["left", "c", "a", "v", "z"],
    "A2", ["left", "c", "a", "z"],
    "Bb2", ["left", "up", "c", "a", "v", "z"],
    "B2", ["left", "up", "c", "a", "z"],
    "C3", ["a", "z"],
    "Db3", ["right", "a", "v", "z"],
    "D3", ["right", "a", "z"],
    "Eb3", ["right", "down", "a", "v", "z"],
    "E3", ["right", "down", "a", "z"],
    "F3", ["down", "a", "z"],
    "Gb3", ["left", "down", "a", "v", "z"],
    "G3", ["left", "down", "a", "z"],
    "Ab3", ["left", "a", "v", "z"],
    "A3", ["left", "a", "z"],
    "Bb3", ["left", "up", "a", "v", "z"],
    "B3", ["left", "up", "a", "z"],
    "C4", ["c", "z"],
    "Db4", ["right", "c", "v", "z"],
    "D4", ["right", "c", "z"],
    "Eb4", ["right", "down", "c", "v", "z"],
    "E4", ["right", "down", "c", "z"],
    "F4", ["down", "c", "z"],
    "Gb4", ["left", "down", "c", "v", "z"],
    "G4", ["left", "down", "c", "z"],
    "Ab4", ["left", "c", "v", "z"],
    "A4", ["left", "c", "z"],
    "Bb4", ["left", "up", "c", "v", "z"],
    "B4", ["left", "up", "c", "z"],
    "C5", ["z"],
    "Db5", ["right", "v", "z"],
    "D5", ["right", "z"],
    "Eb5", ["right", "down", "v", "z"],
    "E5", ["right", "down", "z"],
    "F5", ["down", "z"],
    "Gb5", ["left", "down", "v", "z"],
    "G5", ["left", "down", "z"],
    "Ab5", ["left", "v", "z"],
    "A5", ["left", "z"],
    "Bb5", ["left", "up", "v", "z"],
    "B5", ["left", "up", "z"],
    "C6", ["c", "d", "z"],
    "Db6", ["right", "c", "d", "v", "z"],
    "D6", ["right", "c", "d", "z"],
    "Eb6", ["right", "down", "c", "d", "v", "z"],
    "E6", ["right", "down", "c", "d", "z"],
    "F6", ["down", "c", "d", "z"],
    "Gb6", ["left", "down", "c", "d", "v", "z"],
    "G6", ["left", "down", "c", "d", "z"],
    "Ab6", ["left", "c", "d", "v", "z"],
    "A6", ["left", "c", "d", "z"],
    "Bb6", ["left", "up", "c", "d", "v", "z"],
    "B6", ["left", "up", "c", "d", "z"],
    "C7", ["d", "z"],
    "Db7", ["right", "d", "v", "z"],
    "D7", ["right", "d", "z"],
    "Eb7", ["right", "down", "d", "v", "z"],
    "E7", ["right", "down", "d", "z"],
    "F7", ["down", "d", "z"],
    "Gb7", ["left", "down", "d", "v", "z"],
    "G7", ["left", "down", "d", "z"],
    "Ab7", ["left", "d", "v", "z"],
    "A7", ["left", "d", "z"],
    "Bb7", ["left", "up", "d", "v", "z"],
    "B7", ["left", "up", "d", "z"],
    "C8", ["up", "d", "z"],

    "Db8", ["right", "c", "w", "v", "z"],
    "D8", ["right", "c", "w", "z"],
    "Eb8", ["right", "down", "c", "w", "v", "z"],
    "E8", ["right", "down", "c", "w", "z"],
    "F8", ["down", "c", "w", "z"],
    "Gb8", ["left", "down", "c", "w", "v", "z"],
    "G8", ["left", "down", "c", "w", "z"],
    "Ab8", ["left", "c", "w", "v", "z"],
    "A8", ["left", "c", "w", "z"],
    "Bb8", ["left", "up", "c", "w", "v", "z"],
    "B8", ["left", "up", "c", "w", "z"],
    "C9", ["w", "z"],
    "Db9", ["right", "w", "v", "z"],
    "D9", ["right", "w", "z"],
    "Eb9", ["right", "down", "w", "v", "z"],
    "E9", ["right", "down", "w", "z"],
    "F9", ["down", "w", "z"],
    "Gb9", ["left", "down", "w", "v", "z"],
    "G9", ["left", "down", "w", "z"],
    "Ab9", ["left", "w", "v", "z"],
    "A9", ["left", "w", "z"],
    "Bb9", ["left", "up", "w", "v", "z"],
    "B9", ["left", "up", "w", "z"],
    "C10", ["up", "w", "z"]
)

DebounceCheck() {
    global LastKeyTime, DebounceDelay
    currentTime := A_TickCount
    if (currentTime - LastKeyTime < DebounceDelay)
        return false
    LastKeyTime := currentTime
    return true
}

SwitchInstrument(number) {
    global InstrumentKeyHoldMs, InstrumentKeyGapMs, LastInstrumentNumber
    number := String(number)
    if (number = LastInstrumentNumber)
        return 0

    startedAt := A_TickCount
    digitCount := StrLen(number)
    SendEvent("{b down}")
    Sleep(InstrumentKeyHoldMs)
    Loop Parse, number {
        SendEvent("{" A_LoopField " down}")
        Sleep(InstrumentKeyHoldMs)
        SendEvent("{" A_LoopField " up}")
        if (A_Index < digitCount)
            Sleep(InstrumentKeyGapMs)
    }
    SendEvent("{b up}")
    Sleep(InstrumentKeyGapMs)
    LastInstrumentNumber := number
    return A_TickCount - startedAt
}

WaitUntil(deadline) {
    remaining := deadline - A_TickCount
    if (remaining > 0)
        Sleep(remaining)
}

PressRecordingHotkey() {
    global RecordingHotkey, RecordingKeyHoldMs
    ; OBS polls global hotkey state and can miss an instantaneous SendInput tap.
    ; SendEvent with an explicit hold behaves like a physical key press.
    SendEvent("{" RecordingHotkey " down}")
    Sleep(RecordingKeyHoldMs)
    SendEvent("{" RecordingHotkey " up}")
}

StartRecording() {
    global ControlRecording, RecordingActive
    if !ControlRecording || RecordingActive
        return
    PressRecordingHotkey()
    RecordingActive := true
}

StopRecording() {
    global ControlRecording, RecordingActive
    if !ControlRecording || !RecordingActive
        return
    PressRecordingHotkey()
    RecordingActive := false
}

WatchStopRequest() {
    global StopRequestFile
    if (StopRequestFile != "" && FileExist(StopRequestFile)) {
        try FileDelete(StopRequestFile)
        ExitApp()
    }
}

PressNote(keys) {
    global ActiveKeys
    SetKeyDelay(10)
    ActiveKeys := []
    for key in keys {
        Send("{" key " down}")
        ActiveKeys.Push(key)
    }
}

ReleaseNote(keys) {
    global ActiveKeys
    SetKeyDelay(10)
    for key in keys
        Send("{" key " up}")
    ActiveKeys := []
}

ReleaseActiveKeys() {
    global ActiveKeys
    SetKeyDelay(1)
    for key in ActiveKeys
        Send("{" key " up}")
    ActiveKeys := []
}

HandleExit(exitReason := "", exitCode := 0) {
    global StopRequestFile
    ReleaseActiveKeys()
    StopRecording()
    if (StopRequestFile != "" && FileExist(StopRequestFile))
        try FileDelete(StopRequestFile)
}

PlaySequenceFromFile(path) {
    global ChangeInstruments, Language, Notes, NoteDelay, SelectedChannel, LastInstrumentNumber
    if !FileExist(path) {
        message := Language = "en" ? "Sequence file not found:`n" path : "Файл последовательности не найден:`n" path
        MsgBox(message, "DELTARUNE Piano", "Iconx")
        return
    }

    timelineStart := A_TickCount
    elapsed := 0
    channelMatches := true
    LastInstrumentNumber := ""
    Loop Read, path {
        line := Trim(A_LoopReadLine)
        if (line = "" || SubStr(line, 1, 1) = ";")
            continue

        if RegExMatch(line, "i)^\[Channel\s+(\d+)\]$", &channelMatch) {
            channelMatches := SelectedChannel = 0 || channelMatch[1] + 0 = SelectedChannel
            continue
        }
        if !channelMatches
            continue

        parts := StrSplit(line, "|")
        if (parts.Length != 2)
            continue

        command := Trim(parts[1])
        value := Trim(parts[2])

        if (command = "Sleep") {
            elapsed += value + 0
            WaitUntil(timelineStart + elapsed)
        }
        else if (command = "Instrument") {
            if ChangeInstruments
                timelineStart += SwitchInstrument(value)
        }
        else if Notes.Has(command) {
            pressDuration := value + 0
            if (pressDuration < 1)
                pressDuration := 1
            PressNote(Notes[command])
            elapsed += pressDuration
            WaitUntil(timelineStart + elapsed)
            ReleaseNote(Notes[command])
            elapsed += NoteDelay
            WaitUntil(timelineStart + elapsed)
        }
    }
}

SetTimer(WatchStopRequest, 50)
OnExit(HandleExit)

$F9:: {
    global IsPlaying, SequenceFile
    if IsPlaying || !DebounceCheck()
        return

    IsPlaying := true
    StartRecording()
    try PlaySequenceFromFile(SequenceFile)
    finally {
        ReleaseActiveKeys()
        StopRecording()
        IsPlaying := false
    }
}


$F10:: {
    ExitApp()
}
