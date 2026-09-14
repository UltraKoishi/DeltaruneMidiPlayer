using System.Buffers.Binary;
using DeltaruneMidiPlayer.Models;

namespace DeltaruneMidiPlayer.Services;

internal static class MidiConverter
{
    private const int DefaultTempoMicrosecondsPerQuarter = 500_000;
    private const int LowestPlayableNote = 0;
    private const int HighestPlayableNote = 120;
    private const string PlayableNoteRange = "C0–C10";

    private static readonly string[] NoteNames =
        ["C", "Db", "D", "Eb", "E", "F", "Gb", "G", "Ab", "A", "Bb", "B"];

    private sealed record MidiMessage(long Tick, int Order, byte Status, byte Data1, byte Data2);
    private sealed record TempoEvent(long Tick, int Order, int MicrosecondsPerQuarter);
    private sealed record RawNote(long StartTick, long EndTick, int Note, int Channel);
    private sealed record TimedCommand(SequenceCommand Command, long StartTick, int Priority);
    private sealed record TempoPoint(long Tick, double Seconds, int MicrosecondsPerQuarter);

    private sealed class TempoMap
    {
        private readonly ushort _division;
        private readonly TempoPoint[] _points;

        public TempoMap(IEnumerable<TempoEvent> events, ushort division, double? bpmOverride)
        {
            _division = division;

            var normalizedEvents = (bpmOverride.HasValue ? [] : events)
                .OrderBy(e => e.Tick)
                .ThenBy(e => e.Order)
                .GroupBy(e => e.Tick)
                .Select(group => group.Last())
                .ToArray();

            var tempoAtZero = bpmOverride.HasValue
                ? checked((int)Math.Round(60_000_000.0 / bpmOverride.Value, MidpointRounding.AwayFromZero))
                : normalizedEvents
                    .Where(e => e.Tick == 0)
                    .Select(e => e.MicrosecondsPerQuarter)
                    .DefaultIfEmpty(DefaultTempoMicrosecondsPerQuarter)
                    .Last();

            var points = new List<TempoPoint>
            {
                new(0, 0, tempoAtZero)
            };

            long previousTick = 0;
            double previousSeconds = 0;
            var previousTempo = tempoAtZero;

            foreach (var tempoEvent in normalizedEvents.Where(e => e.Tick > 0))
            {
                if (tempoEvent.MicrosecondsPerQuarter == previousTempo)
                    continue;

                var eventSeconds = previousSeconds +
                    (tempoEvent.Tick - previousTick) * previousTempo / (double)_division / 1_000_000.0;

                points.Add(new TempoPoint(
                    tempoEvent.Tick,
                    eventSeconds,
                    tempoEvent.MicrosecondsPerQuarter));

                previousTick = tempoEvent.Tick;
                previousSeconds = eventSeconds;
                previousTempo = tempoEvent.MicrosecondsPerQuarter;
            }

            _points = points.ToArray();
        }

        public double InitialBpm => ToBpm(_points[0].MicrosecondsPerQuarter);
        public double MinimumBpm => _points.Min(point => ToBpm(point.MicrosecondsPerQuarter));
        public double MaximumBpm => _points.Max(point => ToBpm(point.MicrosecondsPerQuarter));
        public int ChangeCount => Math.Max(0, _points.Length - 1);

        public double TickToSeconds(long tick)
        {
            if (tick < 0)
                throw new ArgumentOutOfRangeException(nameof(tick));

            var left = 0;
            var right = _points.Length - 1;
            while (left < right)
            {
                var middle = left + (right - left + 1) / 2;
                if (_points[middle].Tick <= tick)
                    left = middle;
                else
                    right = middle - 1;
            }

            var point = _points[left];
            return point.Seconds +
                   (tick - point.Tick) * point.MicrosecondsPerQuarter / (double)_division / 1_000_000.0;
        }

        public long TickToRoundedMilliseconds(long tick) =>
            checked((long)Math.Round(TickToSeconds(tick) * 1000.0, MidpointRounding.AwayFromZero));

        private static double ToBpm(int microsecondsPerQuarter) =>
            60_000_000.0 / microsecondsPerQuarter;
    }

    public static IReadOnlyList<MidiChannelConversion> ConvertChannels(
        string path,
        int noteDelayMs = 50,
        bool english = false,
        double? bpmOverride = null,
        bool suppressSimultaneousNoteWarnings = false)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException(L(english, "MIDI file not found.", "MIDI-файл не найден."), path);
        if (noteDelayMs < 0)
            throw new ArgumentOutOfRangeException(nameof(noteDelayMs));
        if (bpmOverride is <= 0 or > 1000)
            throw new ArgumentOutOfRangeException(nameof(bpmOverride));

        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream);

        if (ReadAscii(reader, 4, english) != "MThd")
            throw new InvalidDataException(L(english,
                "The file does not start with an MThd header.",
                "Файл не начинается с заголовка MThd."));

        var headerLength = ReadUInt32BigEndian(reader);
        if (headerLength < 6)
            throw new InvalidDataException(L(english,
                "Invalid MIDI header length.",
                "Некорректная длина MIDI-заголовка."));

        _ = ReadUInt16BigEndian(reader); // format
        var trackCount = ReadUInt16BigEndian(reader);
        var division = ReadUInt16BigEndian(reader);
        if ((division & 0x8000) != 0)
            throw new NotSupportedException(L(english,
                "SMPTE time division is not supported. Use a MIDI file with ticks per beat.",
                "SMPTE time division пока не поддерживается. Нужен MIDI с ticks-per-beat."));
        if (division == 0)
            throw new InvalidDataException(L(english,
                "Ticks per beat is zero.",
                "Ticks per beat равен нулю."));

        if (headerLength > 6)
            reader.ReadBytes(checked((int)headerLength - 6));

        var messages = new List<MidiMessage>();
        var tempoEvents = new List<TempoEvent>();
        var numerator = 4;
        var timeSignatureRead = false;
        var eventOrder = 0;

        for (var trackIndex = 0; trackIndex < trackCount; trackIndex++)
        {
            if (ReadAscii(reader, 4, english) != "MTrk")
                throw new InvalidDataException(english
                    ? $"Track {trackIndex + 1}: MTrk header not found."
                    : $"Трек {trackIndex + 1}: не найден заголовок MTrk.");

            var trackLength = ReadUInt32BigEndian(reader);
            var trackEnd = checked(stream.Position + trackLength);
            if (trackEnd > stream.Length)
                throw new EndOfStreamException(english
                    ? $"Track {trackIndex + 1} ends before its declared length."
                    : $"Трек {trackIndex + 1} обрывается раньше заявленной длины.");

            long absoluteTick = 0;
            byte runningStatus = 0;

            while (stream.Position < trackEnd)
            {
                absoluteTick += ReadVariableLength(reader, english);
                var first = reader.ReadByte();
                byte status;
                byte? firstData = null;

                if (first < 0x80)
                {
                    if (runningStatus == 0)
                        throw new InvalidDataException(english
                            ? $"Track {trackIndex + 1}: running status has no previous status byte."
                            : $"Трек {trackIndex + 1}: running status без предыдущего статуса.");
                    status = runningStatus;
                    firstData = first;
                }
                else
                {
                    status = first;
                    if (status < 0xF0)
                        runningStatus = status;
                }

                var order = eventOrder++;

                if (status == 0xFF)
                {
                    runningStatus = 0;
                    var metaType = reader.ReadByte();
                    var length = ReadVariableLength(reader, english);
                    if (length > int.MaxValue)
                        throw new InvalidDataException(L(english,
                            "The MIDI meta event is too large.",
                            "Слишком большое meta-событие."));
                    var data = reader.ReadBytes((int)length);
                    if (data.Length != (int)length)
                        throw new EndOfStreamException(L(english,
                            "The MIDI file ended inside a meta event.",
                            "MIDI оборван внутри meta-события."));

                    if (metaType == 0x51 && data.Length == 3)
                    {
                        var microsecondsPerQuarter = (data[0] << 16) | (data[1] << 8) | data[2];
                        if (microsecondsPerQuarter <= 0)
                            throw new InvalidDataException(english
                                ? $"Invalid tempo event at tick {absoluteTick}."
                                : $"Некорректное событие темпа в тике {absoluteTick}.");
                        tempoEvents.Add(new TempoEvent(absoluteTick, order, microsecondsPerQuarter));
                    }
                    else if (metaType == 0x58 && data.Length >= 1 && !timeSignatureRead)
                    {
                        numerator = data[0] == 0 ? 1 : data[0];
                        timeSignatureRead = true;
                    }

                    continue;
                }

                if (status is 0xF0 or 0xF7)
                {
                    runningStatus = 0;
                    var length = ReadVariableLength(reader, english);
                    SkipExactly(stream, length, english);
                    continue;
                }

                if (status >= 0xF0)
                    throw new InvalidDataException(english
                        ? $"Unsupported system MIDI event 0x{status:X2}."
                        : $"Неподдерживаемое системное MIDI-событие 0x{status:X2}.");

                var type = status & 0xF0;
                var hasData2 = type is not (0xC0 or 0xD0);
                var data1 = firstData ?? reader.ReadByte();
                var data2 = hasData2 ? reader.ReadByte() : (byte)0;
                messages.Add(new MidiMessage(absoluteTick, order, status, data1, data2));
            }

            stream.Position = trackEnd;
        }

        messages.Sort((a, b) => a.Tick != b.Tick ? a.Tick.CompareTo(b.Tick) : a.Order.CompareTo(b.Order));
        var tempoMap = new TempoMap(tempoEvents, division, bpmOverride);

        var active = new Dictionary<(int Channel, int Note), (long Tick, int Velocity)>();
        var rawNotes = new List<RawNote>();
        foreach (var message in messages)
        {
            var type = message.Status & 0xF0;
            var channel = message.Status & 0x0F;
            var key = (channel, (int)message.Data1);

            if (type == 0x90 && message.Data2 > 0)
            {
                active[key] = (message.Tick, message.Data2);
            }
            else if (type == 0x80 || (type == 0x90 && message.Data2 == 0))
            {
                if (active.Remove(key, out var start) && message.Tick > start.Tick)
                    rawNotes.Add(new RawNote(start.Tick, message.Tick, message.Data1, channel));
            }
        }

        if (rawNotes.Count == 0)
            throw new InvalidDataException(L(english,
                "No completed Note On/Note Off notes were found in the MIDI file.",
                "В MIDI не найдено завершённых нот Note On/Note Off."));

        var results = new List<MidiChannelConversion>();
        var validationErrors = new List<string>();
        var validationWarnings = new List<string>();

        foreach (var channel in rawNotes.Select(note => note.Channel).Distinct().Order())
        {
            try
            {
                var result = ConvertChannel(
                    channel,
                    rawNotes,
                    messages,
                    division,
                    numerator,
                    tempoMap,
                    noteDelayMs,
                    english,
                    suppressSimultaneousNoteWarnings);
                results.Add(new MidiChannelConversion(channel + 1, result));
                validationWarnings.AddRange(result.Warnings.Select(warning =>
                    ChannelPrefix(channel, english) + warning));
            }
            catch (MidiValidationException ex)
            {
                validationErrors.AddRange(ex.Errors.Select(error => ChannelPrefix(channel, english) + error));
                validationWarnings.AddRange(ex.Warnings.Select(warning => ChannelPrefix(channel, english) + warning));
            }
        }

        if (validationErrors.Count > 0)
            throw new MidiValidationException(validationErrors, validationWarnings);

        return results;
    }

    private static ConversionResult ConvertChannel(
        int channel,
        IReadOnlyList<RawNote> rawNotes,
        IReadOnlyList<MidiMessage> messages,
        ushort division,
        int numerator,
        TempoMap tempoMap,
        int noteDelayMs,
        bool english,
        bool suppressSimultaneousNoteWarnings)
    {
        var channelNotes = rawNotes.Where(note => note.Channel == channel).ToArray();
        var warnings = new List<string>();
        var validationErrors = new List<string>();
        var playableNotes = ResolveMonophony(
            channelNotes,
            division,
            numerator,
            tempoMap,
            warnings,
            validationErrors,
            english,
            suppressSimultaneousNoteWarnings);

        var unsupportedNotes = channelNotes
            .Select(note => note.Note)
            .Distinct()
            .Where(note => note < LowestPlayableNote || note > HighestPlayableNote)
            .Order()
            .Select(NoteLabel)
            .ToArray();
        if (unsupportedNotes.Length > 0)
            validationErrors.Add(english
                ? "The DELTARUNE AHK key map does not contain these notes: " + string.Join(", ", unsupportedNotes) +
                  $". Supported range: {PlayableNoteRange}."
                : "AHK-карта DELTARUNE не содержит ноты: " + string.Join(", ", unsupportedNotes) +
                  $". Поддерживаемый диапазон: {PlayableNoteRange}.");

        if (validationErrors.Count > 0)
            throw new MidiValidationException(validationErrors, warnings);

        var timePoints = playableNotes
            .SelectMany(note => new[] { note.StartTick, note.EndTick })
            .Append(0)
            .Distinct()
            .Order()
            .ToArray();

        var timed = new List<TimedCommand>();
        var ticksPerBar = (long)division * numerator;

        for (var i = 0; i < timePoints.Length - 1; i++)
        {
            var start = timePoints[i];
            var end = timePoints[i + 1];
            var durationLong = tempoMap.TickToRoundedMilliseconds(end) -
                               tempoMap.TickToRoundedMilliseconds(start);
            if (durationLong <= 0)
                durationLong = 1;
            if (durationLong > int.MaxValue)
                throw new InvalidDataException(L(english,
                    "A MIDI interval is too long for the sequence format.",
                    "Слишком длинный интервал MIDI для формата последовательности."));
            var duration = (int)durationLong;

            var note = playableNotes.FirstOrDefault(item => item.StartTick <= start && item.EndTick >= end);
            if (note is null)
            {
                timed.Add(new TimedCommand(
                    new SequenceCommand(SequenceCommandType.Sleep, "Sleep", duration),
                    start,
                    1));
                continue;
            }

            var label = NoteLabel(note.Note);
            var pressMs = duration - noteDelayMs;
            if (pressMs < 1)
            {
                var bar = start / ticksPerBar + 1;
                var beat = (start % ticksPerBar) / (double)division + 1;
                warnings.Add(english
                    ? $"⚠ Note {label}: event {timed.Count + 1}, measure {bar}, beat {beat:F2}, " +
                      $"time {tempoMap.TickToSeconds(start):F3} sec; duration {duration} ms is less than or equal to the " +
                      $"{noteDelayMs} ms note delay. The key press duration was set to 1 ms."
                    : $"⚠ Нота {label}: событие {timed.Count + 1}, такт {bar}, доля {beat:F2}, " +
                      $"время {tempoMap.TickToSeconds(start):F3} сек; длительность {duration} мс меньше/равна задержке " +
                      $"{noteDelayMs} мс. Длительность нажатия установлена в 1 мс.");
                pressMs = 1;
            }

            timed.Add(new TimedCommand(
                new SequenceCommand(SequenceCommandType.Note, label, pressMs),
                start,
                1));
        }

        int? currentInstrument = null;
        foreach (var message in messages.Where(message =>
                     (message.Status & 0xF0) == 0xC0 && (message.Status & 0x0F) == channel))
        {
            var instrument = message.Data1 + 1;
            if (currentInstrument == instrument)
                continue;

            currentInstrument = instrument;
            timed.Add(new TimedCommand(
                new SequenceCommand(SequenceCommandType.Instrument, instrument.ToString(), 0),
                message.Tick,
                0));
        }

        var commands = timed
            .OrderBy(item => item.StartTick)
            .ThenBy(item => item.Priority)
            .Select(item => item.Command)
            .ToArray();

        return new ConversionResult(
            commands,
            warnings,
            tempoMap.InitialBpm,
            tempoMap.MinimumBpm,
            tempoMap.MaximumBpm,
            tempoMap.ChangeCount,
            messages.Count(message => (message.Status & 0x0F) == channel));
    }

    private static string ChannelPrefix(int zeroBasedChannel, bool english) =>
        english ? $"MIDI channel {zeroBasedChannel + 1}: " : $"MIDI-канал {zeroBasedChannel + 1}: ";

    public static void SaveSequence(string path, IEnumerable<SequenceCommand> commands)
    {
        var lines = commands.Select(command => command.Type switch
        {
            SequenceCommandType.Note => $"{command.Value}|{command.DurationMs}",
            SequenceCommandType.Sleep => $"Sleep|{command.DurationMs}",
            SequenceCommandType.Instrument => $"Instrument|{command.Value}",
            _ => throw new ArgumentOutOfRangeException()
        });
        File.WriteAllLines(path, lines, new System.Text.UTF8Encoding(false));
    }

    private static List<RawNote> ResolveMonophony(
        IEnumerable<RawNote> sourceNotes,
        ushort division,
        int numerator,
        TempoMap tempoMap,
        List<string> warnings,
        List<string> errors,
        bool english,
        bool suppressSimultaneousNoteWarnings)
    {
        var resolved = new List<RawNote>();
        var validationSpans = new List<RawNote>();
        var uniqueNotes = sourceNotes
            .Distinct()
            .OrderBy(n => n.StartTick)
            .ThenBy(n => n.EndTick)
            .ThenBy(n => n.Note)
            .ToArray();

        foreach (var group in uniqueNotes.GroupBy(n => n.StartTick).OrderBy(g => g.Key))
        {
            var simultaneous = group.ToArray();
            if (simultaneous.Length == 1)
            {
                resolved.Add(simultaneous[0]);
                validationSpans.Add(simultaneous[0]);
                continue;
            }

            if (simultaneous.All(n => n.EndTick == simultaneous[0].EndTick))
            {
                var selected = simultaneous.MaxBy(n => n.Note)!;
                resolved.Add(selected);
                validationSpans.Add(selected);

                if (!suppressSimultaneousNoteWarnings)
                {
                    var skipped = simultaneous
                        .Where(n => n != selected)
                        .Select(n => NoteLabel(n.Note));
                    warnings.Add(english
                        ? $"⚠ Simultaneous notes at {FormatLocation(group.Key, division, numerator, tempoMap, true)}: " +
                          $"{string.Join(", ", simultaneous.Select(n => NoteLabel(n.Note)))}. " +
                          $"Playing the highest note {NoteLabel(selected.Note)}; skipped: {string.Join(", ", skipped)}."
                        : $"⚠ Одновременные ноты в {FormatLocation(group.Key, division, numerator, tempoMap, false)}: " +
                          $"{string.Join(", ", simultaneous.Select(n => NoteLabel(n.Note)))}. " +
                          $"Будет сыграна самая высокая нота {NoteLabel(selected.Note)}; пропущены: {string.Join(", ", skipped)}.");
                }
                continue;
            }

            var notes = string.Join(", ", simultaneous.Select(n =>
                $"{NoteLabel(n.Note)} ({FormatDuration(n, tempoMap, english)})"));
            errors.Add(english
                ? $"Unsupported polyphony at {FormatLocation(group.Key, division, numerator, tempoMap, true)}: " +
                  $"the notes {notes} start together but end at different times. Any number of notes can be reduced " +
                  "automatically only when their start and end times are identical; the highest note is then selected."
                : $"Неподдерживаемая полифония: в {FormatLocation(group.Key, division, numerator, tempoMap, false)} " +
                  $"одновременно начинаются ноты {notes}, но заканчиваются в разное время. Автоматически допускается " +
                  "любое количество нот только с одинаковым временем начала и окончания; тогда выбирается самая высокая нота.");

            // Keep one representative span so later overlaps can still be found during this validation pass.
            validationSpans.Add(simultaneous
                .OrderByDescending(n => n.EndTick)
                .ThenByDescending(n => n.Note)
                .First());
        }

        validationSpans.Sort((a, b) => a.StartTick != b.StartTick
            ? a.StartTick.CompareTo(b.StartTick)
            : a.EndTick.CompareTo(b.EndTick));

        for (var i = 0; i < validationSpans.Count; i++)
        {
            var previous = validationSpans[i];
            for (var j = i + 1; j < validationSpans.Count; j++)
            {
                var current = validationSpans[j];
                if (current.StartTick >= previous.EndTick)
                    break;

                errors.Add(english
                    ? $"Unsupported note overlap: {NoteLabel(previous.Note)} ({FormatDuration(previous, tempoMap, true)}) " +
                      $"and {NoteLabel(current.Note)} ({FormatDuration(current, tempoMap, true)}) overlap at " +
                      $"{FormatLocation(current.StartTick, division, numerator, tempoMap, true)}. Only fully synchronized " +
                      "notes with identical start and end times are allowed; the highest note is then played."
                    : $"Неподдерживаемое перекрытие нот: {NoteLabel(previous.Note)} " +
                      $"({FormatDuration(previous, tempoMap, false)}) и {NoteLabel(current.Note)} " +
                      $"({FormatDuration(current, tempoMap, false)}) перекрываются в " +
                      $"{FormatLocation(current.StartTick, division, numerator, tempoMap, false)}. Разрешены только полностью " +
                      "синхронные ноты с одинаковым временем начала и окончания; тогда воспроизводится самая высокая нота.");
            }
        }

        return resolved;
    }

    private static string FormatLocation(
        long tick,
        ushort division,
        int numerator,
        TempoMap tempoMap,
        bool english)
    {
        var ticksPerBar = (long)division * numerator;
        var bar = tick / ticksPerBar + 1;
        var beat = (tick % ticksPerBar) / (double)division + 1;
        return english
            ? $"measure {bar}, beat {beat:F2}, {tempoMap.TickToSeconds(tick):F3} sec"
            : $"такте {bar}, доле {beat:F2}, {tempoMap.TickToSeconds(tick):F3} сек";
    }

    private static string FormatDuration(RawNote note, TempoMap tempoMap, bool english)
    {
        var start = tempoMap.TickToSeconds(note.StartTick);
        var end = tempoMap.TickToSeconds(note.EndTick);
        return english ? $"{start:F3}–{end:F3} sec" : $"{start:F3}–{end:F3} сек";
    }

    private static string NoteLabel(int midiNumber) => $"{NoteNames[midiNumber % 12]}{midiNumber / 12}";

    private static string L(bool english, string englishText, string russianText) =>
        english ? englishText : russianText;

    private static string ReadAscii(BinaryReader reader, int count, bool english)
    {
        var data = reader.ReadBytes(count);
        if (data.Length != count)
            throw new EndOfStreamException(L(english, "Unexpected end of MIDI file.", "Неожиданный конец MIDI-файла."));
        return System.Text.Encoding.ASCII.GetString(data);
    }

    private static ushort ReadUInt16BigEndian(BinaryReader reader)
    {
        Span<byte> data = stackalloc byte[2];
        if (reader.Read(data) != 2)
            throw new EndOfStreamException();
        return BinaryPrimitives.ReadUInt16BigEndian(data);
    }

    private static uint ReadUInt32BigEndian(BinaryReader reader)
    {
        Span<byte> data = stackalloc byte[4];
        if (reader.Read(data) != 4)
            throw new EndOfStreamException();
        return BinaryPrimitives.ReadUInt32BigEndian(data);
    }

    private static long ReadVariableLength(BinaryReader reader, bool english)
    {
        long value = 0;
        for (var i = 0; i < 4; i++)
        {
            var b = reader.ReadByte();
            value = (value << 7) | (uint)(b & 0x7F);
            if ((b & 0x80) == 0)
                return value;
        }
        throw new InvalidDataException(L(english, "Invalid MIDI variable-length quantity.", "Некорректное variable-length MIDI-число."));
    }

    private static void SkipExactly(Stream stream, long count, bool english)
    {
        if (count < 0 || stream.Position + count > stream.Length)
            throw new EndOfStreamException(L(english, "The MIDI file ended inside a SysEx event.", "MIDI оборван внутри SysEx-события."));
        stream.Position += count;
    }
}
