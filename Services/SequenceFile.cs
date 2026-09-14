using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using DeltaruneMidiPlayer.Models;

namespace DeltaruneMidiPlayer.Services;

internal static class SequenceFile
{
    private const string FileHeader = "; Deltarune MIDI Player sequence v2";

    private static readonly Regex ChannelHeaderPattern = new(
        @"^\[Channel\s+(\d+)\]$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex LegacyChannelFilePattern = new(
        @"_ch(\d{1,2})$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static void Save(string path, IReadOnlyList<MidiChannelConversion> channels)
    {
        if (channels.Count == 0)
            throw new ArgumentException("At least one MIDI channel is required.", nameof(channels));

        using var writer = new StreamWriter(path, false, new UTF8Encoding(false));
        writer.WriteLine(FileHeader);

        for (var index = 0; index < channels.Count; index++)
        {
            var channel = channels[index];
            if (index > 0)
                writer.WriteLine();

            writer.WriteLine($"[Channel {channel.MidiChannel}]");
            foreach (var command in channel.Result.Commands)
                writer.WriteLine(FormatCommand(command));
        }
    }

    public static IReadOnlyList<SequenceChannelInfo> ReadChannels(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Sequence file not found.", path);

        var channels = new List<SequenceChannelInfo>();
        int? currentChannel = null;
        int? instrumentNumber = null;
        var noteCount = 0;
        var hasChannelHeaders = false;

        void FinishCurrentChannel()
        {
            if (currentChannel.HasValue)
                channels.Add(new SequenceChannelInfo(currentChannel.Value, instrumentNumber, noteCount));
        }

        foreach (var sourceLine in File.ReadLines(path))
        {
            var line = sourceLine.Trim();
            var channelMatch = ChannelHeaderPattern.Match(line);
            if (channelMatch.Success)
            {
                FinishCurrentChannel();
                currentChannel = int.Parse(channelMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                instrumentNumber = null;
                noteCount = 0;
                hasChannelHeaders = true;
                continue;
            }

            if (line.Length == 0 || line.StartsWith(';'))
                continue;

            if (!hasChannelHeaders && !currentChannel.HasValue)
                currentChannel = InferLegacyChannel(path);
            if (!currentChannel.HasValue)
                continue;

            var parts = line.Split('|', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2)
                continue;

            if (parts[0].Equals("Instrument", StringComparison.OrdinalIgnoreCase))
            {
                if (!instrumentNumber.HasValue &&
                    int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedInstrument))
                {
                    instrumentNumber = parsedInstrument;
                }
            }
            else if (!parts[0].Equals("Sleep", StringComparison.OrdinalIgnoreCase))
            {
                noteCount++;
            }
        }

        FinishCurrentChannel();
        return channels;
    }

    private static int InferLegacyChannel(string path)
    {
        var fileName = Path.GetFileNameWithoutExtension(path);
        var match = LegacyChannelFilePattern.Match(fileName);
        return match.Success && int.TryParse(match.Groups[1].Value, out var channel)
            ? channel
            : 1;
    }

    private static string FormatCommand(SequenceCommand command) => command.Type switch
    {
        SequenceCommandType.Note => $"{command.Value}|{command.DurationMs}",
        SequenceCommandType.Sleep => $"Sleep|{command.DurationMs}",
        SequenceCommandType.Instrument => $"Instrument|{command.Value}",
        _ => throw new ArgumentOutOfRangeException(nameof(command))
    };
}
