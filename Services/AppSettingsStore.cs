using DeltaruneMidiPlayer.UI;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DeltaruneMidiPlayer.Services;

internal sealed class AppSettings
{
    public string Language { get; set; } = "ru";
    public ThemeMode Theme { get; set; } = ThemeMode.System;
    public int WindowWidth { get; set; } = 980;
    public int WindowHeight { get; set; } = 760;
    public bool Maximized { get; set; }
    public bool ControlRecording { get; set; }
    public string RecordingHotkey { get; set; } = "Numpad7";
}

internal static class AppSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DeltaruneMidiPlayer",
        "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return new AppSettings();

            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath), JsonOptions)
                           ?? new AppSettings();
            settings.WindowWidth = Math.Clamp(settings.WindowWidth, 820, 3840);
            settings.WindowHeight = Math.Clamp(settings.WindowHeight, 680, 2160);
            settings.Language = settings.Language == "en" ? "en" : "ru";
            if (string.IsNullOrWhiteSpace(settings.RecordingHotkey))
                settings.RecordingHotkey = "Numpad7";
            return settings;
        }
        catch
        {
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            var directory = Path.GetDirectoryName(SettingsPath)!;
            Directory.CreateDirectory(directory);
            var temporaryPath = SettingsPath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings, JsonOptions));
            File.Move(temporaryPath, SettingsPath, true);
        }
        catch
        {
            // UI preferences should never prevent the application from closing.
        }
    }
}
