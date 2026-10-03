namespace DeltaruneMidiPlayer.Models;

internal enum SequenceCommandType
{
    Note,
    Sleep,
    Instrument
}

internal sealed record SequenceCommand(SequenceCommandType Type, string Value, int DurationMs);

internal sealed record ConversionResult(
    IReadOnlyList<SequenceCommand> Commands,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<int> NoteSequence,
    double InitialBpm,
    double MinimumBpm,
    double MaximumBpm,
    int TempoChangeCount,
    int MidiEventCount);

internal sealed record MidiChannelConversion(int MidiChannel, ConversionResult Result);

internal sealed record SequenceChannelInfo(
    int MidiChannel,
    int? InstrumentNumber,
    int NoteCount);
