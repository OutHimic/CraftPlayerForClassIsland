using System.Text.Json;
using CraftPlayer.Models;

namespace CraftPlayer.Services.Storage;

public class SettingsStore
{
    readonly SemaphoreSlim _saveLock = new(1, 1);
    readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    public string ConfigFolder { get; private set; } = "";
    public string SettingsFilePath => Path.Combine(ConfigFolder, "Settings.json");
    public string LibraryFolder => Path.Combine(ConfigFolder, "Library");

    public PluginSettings Settings { get; private set; } = new();

    public void Initialize(string configFolder)
    {
        ConfigFolder = configFolder;
        Directory.CreateDirectory(ConfigFolder);
        Directory.CreateDirectory(LibraryFolder);

        if (!File.Exists(SettingsFilePath))
        {
            Settings = new PluginSettings();
            var json = JsonSerializer.Serialize(Settings, _jsonOptions);
            File.WriteAllText(SettingsFilePath, json);
            return;
        }

        try
        {
            var json = File.ReadAllText(SettingsFilePath);
            Settings = JsonSerializer.Deserialize<PluginSettings>(json, _jsonOptions) ?? new PluginSettings();
        }
        catch
        {
            Settings = new PluginSettings();
        }
    }

    public async Task SaveAsync()
    {
        await _saveLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var json = JsonSerializer.Serialize(Settings, _jsonOptions);
            await File.WriteAllTextAsync(SettingsFilePath, json).ConfigureAwait(false);
        }
        finally
        {
            _saveLock.Release();
        }
    }
}
