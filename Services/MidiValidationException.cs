namespace DeltaruneMidiPlayer.Services;

internal sealed class MidiValidationException : Exception
{
    public MidiValidationException(
        IEnumerable<string> errors,
        IEnumerable<string>? warnings = null)
        : base("MIDI validation failed.")
    {
        Errors = errors.ToArray();
        Warnings = warnings?.ToArray() ?? [];
    }

    public IReadOnlyList<string> Errors { get; }
    public IReadOnlyList<string> Warnings { get; }
}
