using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CraftPlayer.Models;

public class TrackItem : INotifyPropertyChanged
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string FileName { get; set; } = "";
    public string RelativePath { get; set; } = "";
    public string Title { get; set; } = "";
    public string Artist { get; set; } = "";
    public TimeSpan Duration { get; set; } = TimeSpan.Zero;
    public DateTime ImportedAt { get; set; } = DateTime.Now;

    int _sortIndex;
    bool _isPlayedInCycle;
    bool _isLastPlayed;
    public int SortIndex
    {
        get => _sortIndex;
        set
        {
            if (_sortIndex == value) return;
            _sortIndex = value;
            OnPropertyChanged();
        }
    }

    public bool IsPlayedInCycle
    {
        get => _isPlayedInCycle;
        set
        {
            if (_isPlayedInCycle == value) return;
            _isPlayedInCycle = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PlayStateText));
        }
    }

    public bool IsLastPlayed
    {
        get => _isLastPlayed;
        set
        {
            if (_isLastPlayed == value) return;
            _isLastPlayed = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PlayStateText));
        }
    }

    public string PlayStateText => IsLastPlayed ? "上次播放到这里" : IsPlayedInCycle ? "已播放" : "未播放";

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
