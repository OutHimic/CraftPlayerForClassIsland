using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Shared;
using CraftPlayer.Automation.Actions.Settings;
using CraftPlayer.Models;
using CraftPlayer.Services.Storage;

namespace CraftPlayer.Automation.Actions.SettingsControls;

public partial class PlayMusicActionSettingsControl : ActionSettingsControlBase<PlayMusicActionSettings>, INotifyPropertyChanged
{
    readonly SettingsStore _settingsStore;
    PlaylistItemOption? _selectedPlaylistItem;
    decimal _playCountValue = 1;
    decimal _perTrackLimitValue;
    decimal _totalLimitValue;
    public ObservableCollection<PlaylistItemOption> PlaylistItems { get; } = [];

    public PlayMusicActionSettingsControl()
    {
        _settingsStore = IAppHost.TryGetService<SettingsStore>() ?? CreateFallbackStore();
        InitializeComponent();
        DataContext = this;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        SyncNumericValuesFromSettings();
        RefreshPlaylists();
        OnPropertyChanged(nameof(SourceTypeIndex));
        OnPropertyChanged(nameof(HasSourceSelection));
        OnPropertyChanged(nameof(IsFileMode));
        OnPropertyChanged(nameof(IsPlaylistMode));
        OnPropertyChanged(nameof(OrderModeIndex));
        OnPropertyChanged(nameof(PlayCountValue));
        OnPropertyChanged(nameof(PerTrackLimitValue));
        OnPropertyChanged(nameof(TotalLimitValue));
    }

    public int SourceTypeIndex
    {
        get => Settings.SourceType switch
        {
            PlaybackSourceType.File => 1,
            PlaybackSourceType.Playlist => 2,
            _ => 0
        };
        set
        {
            Settings.SourceType = value switch
            {
                1 => PlaybackSourceType.File,
                2 => PlaybackSourceType.Playlist,
                _ => null
            };
            if (Settings.SourceType != PlaybackSourceType.Playlist)
            {
                Settings.PlaylistId = "";
                SelectedPlaylistItem = null;
            }
            if (Settings.SourceType != PlaybackSourceType.File)
            {
                Settings.FilePath = "";
            }
            OnPropertyChanged(nameof(IsFileMode));
            OnPropertyChanged(nameof(IsPlaylistMode));
            OnPropertyChanged(nameof(HasSourceSelection));
            OnPropertyChanged(nameof(FilePath));
            OnPropertyChanged(nameof(PlayCountLabel));
            OnPropertyChanged(nameof(PerTrackLimitLabel));
        }
    }

    public bool HasSourceSelection => Settings.SourceType != null;
    public bool IsFileMode => Settings.SourceType == PlaybackSourceType.File;
    public bool IsPlaylistMode => Settings.SourceType == PlaybackSourceType.Playlist;

    public int OrderModeIndex
    {
        get => (int)Settings.OrderMode;
        set
        {
            Settings.OrderMode = (PlaybackOrderMode)Math.Clamp(value, 0, 2);
            OnPropertyChanged();
        }
    }

    public string FilePath
    {
        get => Settings.FilePath;
        set
        {
            if (Settings.FilePath == value) return;
            Settings.FilePath = value;
            OnPropertyChanged();
        }
    }

    public int PlayCount
    {
        get => (int)_playCountValue;
        set
        {
            _playCountValue = Math.Clamp(value, 1, 9999);
            var settings = TryGetSettings();
            if (settings != null)
            {
                settings.PlayCount = (int)_playCountValue;
            }
            OnPropertyChanged(nameof(PlayCountValue));
            OnPropertyChanged();
        }
    }

    public int PerTrackLimitSeconds
    {
        get => (int)_perTrackLimitValue;
        set
        {
            _perTrackLimitValue = Math.Clamp(value, 0, 86400);
            var settings = TryGetSettings();
            if (settings != null)
            {
                settings.PerTrackLimitSeconds = (int)_perTrackLimitValue;
            }
            OnPropertyChanged(nameof(PerTrackLimitValue));
            OnPropertyChanged();
        }
    }

    public int TotalLimitSeconds
    {
        get => (int)_totalLimitValue;
        set
        {
            _totalLimitValue = Math.Clamp(value, 0, 86400);
            var settings = TryGetSettings();
            if (settings != null)
            {
                settings.TotalLimitSeconds = (int)_totalLimitValue;
            }
            OnPropertyChanged(nameof(TotalLimitValue));
            OnPropertyChanged();
        }
    }

    public decimal PlayCountValue
    {
        get => _playCountValue;
        set => PlayCount = (int)Math.Round(value);
    }

    public decimal PerTrackLimitValue
    {
        get => _perTrackLimitValue;
        set => PerTrackLimitSeconds = (int)Math.Round(value);
    }

    public decimal TotalLimitValue
    {
        get => _totalLimitValue;
        set => TotalLimitSeconds = (int)Math.Round(value);
    }

    public string PlayCountLabel => IsFileMode ? "播放次数" : "单次播放数量（首）";
    public string PerTrackLimitLabel => IsFileMode ? "每次限时（秒，0为不限制）" : "每首限时（秒，0为不限制）";

    public PlaylistItemOption? SelectedPlaylistItem
    {
        get => _selectedPlaylistItem;
        set
        {
            if (Equals(_selectedPlaylistItem, value)) return;
            _selectedPlaylistItem = value;
            Settings.PlaylistId = value?.Id ?? "";
            OnPropertyChanged();
        }
    }

    void RefreshPlaylists()
    {
        PlaylistItems.Clear();
        foreach (var playlist in _settingsStore.Settings.Playlists)
        {
            PlaylistItems.Add(new PlaylistItemOption(playlist.Id, playlist.Name));
        }
        SelectedPlaylistItem = PlaylistItems.FirstOrDefault(x => x.Id == Settings.PlaylistId);
    }

    async void PickFileButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = Avalonia.Controls.TopLevel.GetTopLevel(this);
        if (topLevel == null) return;
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择音频文件",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("音频文件")
                {
                    Patterns = ["*.wav", "*.mp3", "*.ogg", "*.flac"]
                }
            ]
        });

        var file = files.FirstOrDefault();
        if (file == null) return;
        var path = file.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path)) return;
        FilePath = path;
    }

    public new event PropertyChangedEventHandler? PropertyChanged;

    void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    static SettingsStore CreateFallbackStore()
    {
        var store = new SettingsStore();
        store.Initialize(Path.Combine(Path.GetTempPath(), "CraftPlayerFallback"));
        return store;
    }

    void SyncNumericValuesFromSettings()
    {
        var settings = TryGetSettings();
        if (settings == null) return;
        _playCountValue = Math.Clamp(settings.PlayCount, 1, 9999);
        _perTrackLimitValue = Math.Clamp(settings.PerTrackLimitSeconds, 0, 86400);
        _totalLimitValue = Math.Clamp(settings.TotalLimitSeconds, 0, 86400);
    }

    PlayMusicActionSettings? TryGetSettings()
    {
        try
        {
            return Settings;
        }
        catch
        {
            return null;
        }
    }
}

public record PlaylistItemOption(string Id, string Name);
