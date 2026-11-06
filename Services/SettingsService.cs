using System.Text.Json;
using TapOS.Models;

namespace TapOS.Services;

public sealed class SettingsService
{
    private readonly string _settingsPath;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public SettingsService(string? settingsPath = null)
    {
        _settingsPath = settingsPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TapOS",
            "settings.json");
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return new AppSettings();
            }

            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_settingsPath));
            settings ??= new AppSettings();
            if (SoundFolderDefaults.IsBuildOutputPath(settings.SoundFolder))
            {
                var repositorySounds = SoundFolderDefaults.GetDefaultPath();
                if (!SoundFolderDefaults.IsBuildOutputPath(repositorySounds))
                {
                    settings.SoundFolder = repositorySounds;
                }
            }

            return settings;
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        var directory = Path.GetDirectoryName(_settingsPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(_settingsPath, JsonSerializer.Serialize(settings, _jsonOptions));
    }
}
