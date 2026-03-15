using System.ComponentModel;
using System.Runtime.CompilerServices;
using CraftPlayer.Models;

namespace CraftPlayer.Automation.Actions.Settings;

public class PlayMusicActionSettings : INotifyPropertyChanged
{
    PlaybackSourceType? _sourceType;
    string _playlistId = "";
    string _filePath = "";
    int _playCount = 1;
    int _perTrackLimitSeconds;
    int _totalLimitSeconds;
    PlaybackOrderMode _orderMode = PlaybackOrderMode.Sequential;

    public PlaybackSourceType? SourceType
    {
        get => _sourceType;
        set => SetField(ref _sourceType, value);
    }

    public string PlaylistId
    {
        get => _playlistId;
        set => SetField(ref _playlistId, value);
    }

    public string FilePath
    {
        get => _filePath;
        set => SetField(ref _filePath, value);
    }

    public int PlayCount
    {
        get => _playCount;
        set => SetField(ref _playCount, value <= 0 ? 1 : value);
    }

    public int PerTrackLimitSeconds
    {
        get => _perTrackLimitSeconds;
        set => SetField(ref _perTrackLimitSeconds, Math.Max(0, value));
    }

    public int TotalLimitSeconds
    {
        get => _totalLimitSeconds;
        set => SetField(ref _totalLimitSeconds, Math.Max(0, value));
    }

    public PlaybackOrderMode OrderMode
    {
        get => _orderMode;
        set => SetField(ref _orderMode, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
