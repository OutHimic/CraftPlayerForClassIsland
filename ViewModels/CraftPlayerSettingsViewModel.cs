using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using CraftPlayer.Models;
using CraftPlayer.Services.Export;
using CraftPlayer.Services.Playback;
using CraftPlayer.Services.Storage;

namespace CraftPlayer.ViewModels;

public class CraftPlayerSettingsViewModel : INotifyPropertyChanged
{
    readonly SettingsStore _settingsStore;
    readonly LibraryFileService _libraryFileService;
    readonly PlaybackMetadataService _metadataService;
    readonly PlaylistCsvExportService _csvExportService;
    readonly PlaybackEngineService _playbackEngineService;
    readonly Random _random = new();

    public ObservableCollection<Playlist> Playlists { get; } = [];
    public ObservableCollection<TrackItem> Tracks { get; } = [];

    Playlist? _selectedPlaylist;
    TrackItem? _selectedTrack;
    bool _selectedPlaylistLocked;

    public CraftPlayerSettingsViewModel(
        SettingsStore settingsStore,
        LibraryFileService libraryFileService,
        PlaybackMetadataService metadataService,
        PlaylistCsvExportService csvExportService,
        PlaybackEngineService playbackEngineService)
    {
        _settingsStore = settingsStore;
        _libraryFileService = libraryFileService;
        _metadataService = metadataService;
        _csvExportService = csvExportService;
        _playbackEngineService = playbackEngineService;
        Load();
    }

    public bool EnableSmtc
    {
        get => _settingsStore.Settings.EnableSmtc;
        set
        {
            if (_settingsStore.Settings.EnableSmtc == value) return;
            _settingsStore.Settings.EnableSmtc = value;
            _playbackEngineService.RefreshSmtcEnabled();
            _ = SaveAsync();
            OnPropertyChanged();
        }
    }

    public Playlist? SelectedPlaylist
    {
        get => _selectedPlaylist;
        set
        {
            if (_selectedPlaylist == value) return;
            _selectedPlaylist = value;
            OnPropertyChanged();
            RefreshTracks();
            RefreshPlaylistLockState();
        }
    }

    public TrackItem? SelectedTrack
    {
        get => _selectedTrack;
        set
        {
            if (_selectedTrack == value) return;
            _selectedTrack = value;
            OnPropertyChanged();
        }
    }

    public bool SelectedPlaylistLocked
    {
        get => _selectedPlaylistLocked;
        private set
        {
            if (_selectedPlaylistLocked == value) return;
            _selectedPlaylistLocked = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanModifySelectedPlaylist));
        }
    }

    public bool CanModifySelectedPlaylist => SelectedPlaylist != null && !SelectedPlaylistLocked;
    public string PlaylistLockHint => SelectedPlaylistLocked ? "该歌单已锁定，暂不可编辑或打乱。" : "";

    public async Task AddPlaylistAsync(string? name)
    {
        var displayName = string.IsNullOrWhiteSpace(name) ? $"新建歌单 {DateTime.Now:HHmmss}" : name.Trim();
        var playlist = new Playlist
        {
            Name = displayName
        };
        _settingsStore.Settings.Playlists.Add(playlist);
        Playlists.Add(playlist);
        SelectedPlaylist = playlist;
        await SaveAsync();
    }

    public async Task RenameSelectedPlaylistAsync(string? name)
    {
        if (SelectedPlaylist == null || SelectedPlaylistLocked) return;
        if (string.IsNullOrWhiteSpace(name)) return;
        SelectedPlaylist.Name = name.Trim();
        RefreshPlaylists();
        await SaveAsync();
    }

    public async Task DeleteSelectedPlaylistAsync()
    {
        if (SelectedPlaylist == null || SelectedPlaylistLocked) return;
        foreach (var track in SelectedPlaylist.Tracks)
        {
            _libraryFileService.DeleteTrackFileIfExists(track);
        }
        _settingsStore.Settings.Playlists.RemoveAll(x => x.Id == SelectedPlaylist.Id);
        Load();
        await SaveAsync();
    }

    public async Task ImportFilesAsync(IEnumerable<string> paths)
    {
        if (SelectedPlaylist == null || SelectedPlaylistLocked) return;

        foreach (var path in paths)
        {
            if (!_libraryFileService.IsSupportedAudio(path)) continue;
            var importedPath = _libraryFileService.ImportToPlaylist(path, SelectedPlaylist.Id);
            var metadata = await _metadataService.BuildTrackFromFileAsync(importedPath);
            metadata.RelativePath = LibraryFileService.ToRelativePath(_settingsStore.ConfigFolder, importedPath);
            metadata.SortIndex = SelectedPlaylist.Tracks.Count;
            SelectedPlaylist.Tracks.Add(metadata);
        }

        RefreshTracks();
        await SaveAsync();
    }

    public async Task DeleteSelectedTrackAsync()
    {
        if (SelectedPlaylist == null || SelectedTrack == null || SelectedPlaylistLocked) return;
        SelectedPlaylist.Tracks.RemoveAll(x => x.Id == SelectedTrack.Id);
        _libraryFileService.DeleteTrackFileIfExists(SelectedTrack);
        ReindexTracks(SelectedPlaylist);
        RefreshTracks();
        await SaveAsync();
    }

    public async Task MoveSelectedTrackAsync(int offset)
    {
        if (SelectedPlaylist == null || SelectedTrack == null || SelectedPlaylistLocked) return;
        var list = SelectedPlaylist.Tracks.OrderBy(x => x.SortIndex).ToList();
        var index = list.FindIndex(x => x.Id == SelectedTrack.Id);
        if (index < 0) return;
        var target = index + offset;
        if (target < 0 || target >= list.Count) return;

        (list[index], list[target]) = (list[target], list[index]);
        SelectedPlaylist.Tracks = list;
        ReindexTracks(SelectedPlaylist);
        RefreshTracks();
        SelectedTrack = Tracks.FirstOrDefault(x => x.Id == SelectedTrack.Id);
        await SaveAsync();
    }

    public async Task ShuffleTracksAsync()
    {
        if (SelectedPlaylist == null || SelectedPlaylistLocked) return;
        var list = SelectedPlaylist.Tracks.ToList();
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = _random.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }

        SelectedPlaylist.Tracks = list;
        ReindexTracks(SelectedPlaylist);
        RefreshTracks();
        await SaveAsync();
    }

    public async Task ExportCurrentPlaylistCsvAsync(string filePath)
    {
        if (SelectedPlaylist == null) return;
        await _csvExportService.ExportAsync(SelectedPlaylist, filePath);
    }

    public async Task LockSelectedPlaylistAsync()
    {
        if (SelectedPlaylist == null) return;
        if (SelectedPlaylist.IsLocked) return;
        SelectedPlaylist.IsLocked = true;
        RefreshPlaylistLockState();
        await SaveAsync();
    }

    public async Task UnlockSelectedPlaylistAsync()
    {
        if (SelectedPlaylist == null) return;
        if (!SelectedPlaylist.IsLocked) return;
        SelectedPlaylist.IsLocked = false;
        RefreshPlaylistLockState();
        await SaveAsync();
    }

    async Task SaveAsync()
    {
        await _settingsStore.SaveAsync();
    }

    void Load()
    {
        Playlists.Clear();
        foreach (var playlist in _settingsStore.Settings.Playlists)
        {
            ReindexTracks(playlist);
            Playlists.Add(playlist);
        }

        SelectedPlaylist = Playlists.FirstOrDefault();
        RefreshPlaylistLockState();
    }

    void RefreshPlaylists()
    {
        var selectedId = SelectedPlaylist?.Id;
        Playlists.Clear();
        foreach (var playlist in _settingsStore.Settings.Playlists)
        {
            Playlists.Add(playlist);
        }

        SelectedPlaylist = Playlists.FirstOrDefault(x => x.Id == selectedId) ?? Playlists.FirstOrDefault();
        RefreshPlaylistLockState();
    }

    void RefreshTracks()
    {
        Tracks.Clear();
        if (SelectedPlaylist == null) return;
        foreach (var track in SelectedPlaylist.Tracks)
        {
            Tracks.Add(track);
        }
    }

    static void ReindexTracks(Playlist playlist)
    {
        for (var i = 0; i < playlist.Tracks.Count; i++)
        {
            playlist.Tracks[i].SortIndex = i;
        }
    }

    void RefreshPlaylistLockState()
    {
        if (SelectedPlaylist == null)
        {
            SelectedPlaylistLocked = false;
            OnPropertyChanged(nameof(PlaylistLockHint));
            return;
        }

        SelectedPlaylistLocked = SelectedPlaylist.IsLocked;
        OnPropertyChanged(nameof(PlaylistLockHint));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
