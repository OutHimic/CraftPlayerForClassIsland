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

    public bool CanModifySelectedPlaylist => SelectedPlaylist != null;

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
        if (SelectedPlaylist == null) return;
        if (string.IsNullOrWhiteSpace(name)) return;
        SelectedPlaylist.Name = name.Trim();
        RefreshPlaylists();
        await SaveAsync();
    }

    public async Task DeleteSelectedPlaylistAsync()
    {
        if (SelectedPlaylist == null) return;
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
        if (SelectedPlaylist == null) return;

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
        if (SelectedPlaylist == null) return;
        var targetTracks = GetCheckedTracks();
        if (targetTracks.Count == 0) return;

        var selectedTrackId = SelectedTrack?.Id;
        foreach (var track in targetTracks)
        {
            SelectedPlaylist.Tracks.RemoveAll(x => x.Id == track.Id);
            _libraryFileService.DeleteTrackFileIfExists(track);
        }

        if (targetTracks.Any(x => x.Id == SelectedPlaylist.LastPlayedTrackId))
        {
            SelectedPlaylist.LastPlayedTrackId = "";
        }

        ReindexTracks(SelectedPlaylist);
        RefreshTracks();
        SelectedTrack = string.IsNullOrWhiteSpace(selectedTrackId)
            ? null
            : Tracks.FirstOrDefault(x => x.Id == selectedTrackId);
        await SaveAsync();
    }

    public async Task MoveSelectedTrackAsync(int offset)
    {
        if (SelectedPlaylist == null) return;
        var targetTracks = GetCheckedTracks();
        if (targetTracks.Count == 0) return;

        var selectedTrackId = SelectedTrack?.Id;
        var targetIds = targetTracks.Select(x => x.Id).ToHashSet();
        var moved = false;
        if (offset < 0)
        {
            for (var i = 1; i < Tracks.Count; i++)
            {
                if (!targetIds.Contains(Tracks[i].Id) || targetIds.Contains(Tracks[i - 1].Id)) continue;
                Tracks.Move(i, i - 1);
                moved = true;
            }
        }
        else if (offset > 0)
        {
            for (var i = Tracks.Count - 2; i >= 0; i--)
            {
                if (!targetIds.Contains(Tracks[i].Id) || targetIds.Contains(Tracks[i + 1].Id)) continue;
                Tracks.Move(i, i + 1);
                moved = true;
            }
        }

        if (!moved) return;

        SelectedPlaylist.Tracks = Tracks.ToList();
        ReindexTracks(SelectedPlaylist);
        RefreshTracks();
        SelectedTrack = string.IsNullOrWhiteSpace(selectedTrackId)
            ? null
            : Tracks.FirstOrDefault(x => x.Id == selectedTrackId);
        await SaveAsync();
    }

    public async Task ShuffleTracksAsync()
    {
        if (SelectedPlaylist == null) return;
        var list = Tracks.ToList();
        var checkedTracks = GetCheckedTracks();
        var tracksToShuffle = checkedTracks.Count > 0
            ? checkedTracks
            : list.Where(x => !x.IsPlayedInCycle).ToList();
        if (tracksToShuffle.Count < 2) return;

        var shuffledTracks = tracksToShuffle.ToList();
        for (var i = shuffledTracks.Count - 1; i > 0; i--)
        {
            var j = _random.Next(i + 1);
            (shuffledTracks[i], shuffledTracks[j]) = (shuffledTracks[j], shuffledTracks[i]);
        }

        var targetIds = tracksToShuffle.Select(x => x.Id).ToHashSet();
        var shuffledIndex = 0;
        for (var i = 0; i < list.Count; i++)
        {
            if (!targetIds.Contains(list[i].Id)) continue;
            list[i] = shuffledTracks[shuffledIndex++];
        }

        ApplyTrackOrder(list);
        await SaveAsync();
    }

    public async Task ClearPlayedStateAsync()
    {
        if (SelectedPlaylist == null) return;
        var targetTracks = GetCheckedTracks();
        if (targetTracks.Count == 0) return;

        var targetIds = targetTracks.Select(x => x.Id).ToHashSet();
        foreach (var track in SelectedPlaylist.Tracks.Where(x => targetIds.Contains(x.Id)))
        {
            track.IsPlayedInCycle = false;
            track.IsLastPlayed = false;
        }

        if (targetIds.Contains(SelectedPlaylist.LastPlayedTrackId) ||
            !SelectedPlaylist.Tracks.Any(x => x.IsLastPlayed))
        {
            SelectedPlaylist.LastPlayedTrackId = "";
        }

        await SaveAsync();
    }

    public void CheckAllTracks()
    {
        foreach (var track in Tracks)
        {
            track.IsChecked = true;
        }
    }

    public void ClearTrackChecks()
    {
        foreach (var track in Tracks)
        {
            track.IsChecked = false;
        }
    }

    public async Task ExportCurrentPlaylistCsvAsync(string filePath)
    {
        if (SelectedPlaylist == null) return;
        await _csvExportService.ExportAsync(SelectedPlaylist, filePath);
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
    }

    void RefreshTracks()
    {
        Tracks.Clear();
        if (SelectedPlaylist == null) return;
        foreach (var track in SelectedPlaylist.Tracks.OrderBy(x => x.SortIndex))
        {
            Tracks.Add(track);
        }
    }

    List<TrackItem> GetCheckedTracks() =>
        Tracks.Where(x => x.IsChecked).ToList();

    void ApplyTrackOrder(IReadOnlyList<TrackItem> orderedTracks)
    {
        for (var targetIndex = 0; targetIndex < orderedTracks.Count; targetIndex++)
        {
            var currentIndex = Tracks.IndexOf(orderedTracks[targetIndex]);
            if (currentIndex >= 0 && currentIndex != targetIndex)
            {
                Tracks.Move(currentIndex, targetIndex);
            }
        }

        if (SelectedPlaylist == null) return;
        SelectedPlaylist.Tracks = Tracks.ToList();
        ReindexTracks(SelectedPlaylist);
    }

    static void ReindexTracks(Playlist playlist)
    {
        for (var i = 0; i < playlist.Tracks.Count; i++)
        {
            playlist.Tracks[i].SortIndex = i;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
