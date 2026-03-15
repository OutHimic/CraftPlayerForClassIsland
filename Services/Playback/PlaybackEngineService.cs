using CraftPlayer.Models;
using CraftPlayer.Services.Automation;
using CraftPlayer.Services.Storage;
using Avalonia.Threading;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace CraftPlayer.Services.Playback;

public class PlaybackEngineService(
    SettingsStore settingsStore,
    LibraryFileService libraryFileService,
    PlaybackMetadataService metadataService,
    PlaybackAutomationBridge automationBridge,
    SmtcBridgeService smtcBridgeService)
{
    readonly MediaPlayer _mediaPlayer = new();
    readonly SemaphoreSlim _sessionLock = new(1, 1);
    readonly Random _random = new();

    List<(TrackItem Track, string AbsolutePath)> _queue = [];
    Playlist? _currentPlaylist;
    int _index = -1;
    bool _isStartedRaised;
    bool _isStopping;
    int _perTrackLimitSeconds;
    CancellationTokenSource? _sessionLimitCts;
    CancellationTokenSource? _trackLimitCts;

    public bool IsPlaying { get; private set; }

    public event EventHandler? StateChanged;

    public void Initialize()
    {
        _mediaPlayer.MediaEnded += MediaPlayerOnMediaEnded;
        _mediaPlayer.MediaFailed += MediaPlayerOnMediaFailed;
        _mediaPlayer.PlaybackSession.PlaybackStateChanged += PlaybackSessionOnPlaybackStateChanged;
        smtcBridgeService.UpdateEnabled(_mediaPlayer);
    }

    public async Task StartAsync(PlaybackRequest request)
    {
        await _sessionLock.WaitAsync();
        try
        {
            await StopInternalAsync(false);

            _queue = await BuildQueueAsync(request);
            if (_queue.Count == 0)
            {
                IsPlaying = false;
                RaiseStateChanged();
                return;
            }

            IsPlaying = true;
            _isStartedRaised = false;
            _index = 0;
            _isStopping = false;
            _perTrackLimitSeconds = Math.Max(0, request.PerTrackLimitSeconds);
            StartSessionLimit(request.TotalLimitSeconds);
            await PlayCurrentAsync(_perTrackLimitSeconds);
        }
        finally
        {
            _sessionLock.Release();
        }
    }

    public async Task StopAsync()
    {
        await _sessionLock.WaitAsync();
        try
        {
            await StopInternalAsync(true);
        }
        finally
        {
            _sessionLock.Release();
        }
    }

    public void RefreshSmtcEnabled()
    {
        smtcBridgeService.UpdateEnabled(_mediaPlayer);
    }

    async Task<List<(TrackItem Track, string AbsolutePath)>> BuildQueueAsync(PlaybackRequest request)
    {
        var candidates = new List<(TrackItem Track, string AbsolutePath)>();
        _currentPlaylist = null;

        if (request.SourceType == PlaybackSourceType.File && !string.IsNullOrWhiteSpace(request.FilePath))
        {
            if (File.Exists(request.FilePath))
            {
                var track = await metadataService.BuildTrackFromFileAsync(request.FilePath);
                candidates.Add((track, request.FilePath));
            }
        }

        if (request.SourceType == PlaybackSourceType.Playlist)
        {
            var playlist = settingsStore.Settings.Playlists.FirstOrDefault(x => x.Id == request.PlaylistId);
            if (playlist != null)
            {
                _currentPlaylist = playlist;
                if (!playlist.IsLocked)
                {
                    playlist.IsLocked = true;
                    _ = settingsStore.SaveAsync();
                }
                foreach (var track in BuildPlaylistCandidates(playlist))
                {
                    var absolutePath = libraryFileService.GetAbsolutePath(track);
                    if (File.Exists(absolutePath))
                    {
                        candidates.Add((track, absolutePath));
                    }
                }
            }
        }

        candidates = request.OrderMode switch
        {
            PlaybackOrderMode.Random => Shuffle(candidates),
            PlaybackOrderMode.Reverse => candidates.AsEnumerable().Reverse().ToList(),
            _ => candidates
        };

        if (request.PlayCount > 0)
        {
            candidates = candidates.Take(request.PlayCount).ToList();
        }

        return candidates;
    }

    List<TrackItem> BuildPlaylistCandidates(Playlist playlist)
    {
        var ordered = playlist.Tracks.OrderBy(x => x.SortIndex).ToList();
        if (ordered.Count == 0) return [];

        if (ordered.All(x => x.IsPlayedInCycle))
        {
            ResetPlaylistCycle(playlist);
        }

        var startIndex = 0;
        if (!string.IsNullOrWhiteSpace(playlist.LastPlayedTrackId))
        {
            var idx = ordered.FindIndex(x => x.Id == playlist.LastPlayedTrackId);
            if (idx >= 0)
            {
                startIndex = (idx + 1) % ordered.Count;
            }
        }

        var rotated = ordered.Skip(startIndex).Concat(ordered.Take(startIndex)).ToList();
        var unplayed = rotated.Where(x => !x.IsPlayedInCycle).ToList();
        if (unplayed.Count == 0)
        {
            ResetPlaylistCycle(playlist);
            unplayed = rotated;
        }

        return unplayed;
    }

    void ResetPlaylistCycle(Playlist playlist)
    {
        foreach (var track in playlist.Tracks)
        {
            track.IsPlayedInCycle = false;
            track.IsLastPlayed = false;
        }
        playlist.LastPlayedTrackId = "";
    }

    List<(TrackItem Track, string AbsolutePath)> Shuffle(List<(TrackItem Track, string AbsolutePath)> source)
    {
        var list = source.ToList();
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = _random.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
        return list;
    }

    async Task PlayCurrentAsync(int perTrackLimitSeconds)
    {
        if (_index < 0 || _index >= _queue.Count)
        {
            await StopInternalAsync(true);
            return;
        }

        var current = _queue[_index];
        _mediaPlayer.Source = MediaSource.CreateFromUri(new Uri(current.AbsolutePath));
        _mediaPlayer.Play();
        MarkTrackProgress(current.Track);
        await smtcBridgeService.UpdateNowPlayingAsync(_mediaPlayer, current.Track, current.AbsolutePath);
        smtcBridgeService.UpdatePlaybackStatus(_mediaPlayer, MediaPlaybackStatus.Playing);
        automationBridge.RaiseTrackStarted(current.Track);
        if (!_isStartedRaised)
        {
            _isStartedRaised = true;
            automationBridge.RaiseSessionStarted();
        }

        StartTrackLimit(perTrackLimitSeconds);
        RaiseStateChanged();
    }

    void MarkTrackProgress(TrackItem currentTrack)
    {
        if (_currentPlaylist == null) return;
        if (Dispatcher.UIThread.CheckAccess())
        {
            UpdateTrackProgressCore(currentTrack);
            return;
        }

        Dispatcher.UIThread.Post(() => UpdateTrackProgressCore(currentTrack));
    }

    void UpdateTrackProgressCore(TrackItem currentTrack)
    {
        if (_currentPlaylist == null) return;
        foreach (var track in _currentPlaylist.Tracks)
        {
            track.IsLastPlayed = false;
        }

        currentTrack.IsPlayedInCycle = true;
        currentTrack.IsLastPlayed = true;
        _currentPlaylist.LastPlayedTrackId = currentTrack.Id;
        _ = settingsStore.SaveAsync();
    }

    void StartSessionLimit(int totalLimitSeconds)
    {
        _sessionLimitCts?.Cancel();
        _sessionLimitCts?.Dispose();
        _sessionLimitCts = null;

        if (totalLimitSeconds <= 0) return;

        _sessionLimitCts = new CancellationTokenSource();
        var token = _sessionLimitCts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(totalLimitSeconds), token);
                await StopAsync();
            }
            catch (TaskCanceledException)
            {
            }
        }, token);
    }

    void StartTrackLimit(int perTrackLimitSeconds)
    {
        _trackLimitCts?.Cancel();
        _trackLimitCts?.Dispose();
        _trackLimitCts = null;

        if (perTrackLimitSeconds <= 0) return;

        _trackLimitCts = new CancellationTokenSource();
        var token = _trackLimitCts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(perTrackLimitSeconds), token);
                await SkipNextAsync();
            }
            catch (TaskCanceledException)
            {
            }
        }, token);
    }

    async Task SkipNextAsync()
    {
        await _sessionLock.WaitAsync();
        try
        {
            if (!IsPlaying || _isStopping) return;
            automationBridge.RaiseTrackEnded();
            _index++;
            if (_index >= _queue.Count)
            {
                await StopInternalAsync(true);
                return;
            }

            await PlayCurrentAsync(_perTrackLimitSeconds);
        }
        finally
        {
            _sessionLock.Release();
        }
    }

    async void MediaPlayerOnMediaEnded(MediaPlayer sender, object args) => await AdvanceTrackAsync();

    async void MediaPlayerOnMediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args) => await AdvanceTrackAsync();

    async Task AdvanceTrackAsync()
    {
        await _sessionLock.WaitAsync();
        try
        {
            if (!IsPlaying || _isStopping) return;
            automationBridge.RaiseTrackEnded();
            _index++;
            if (_index >= _queue.Count)
            {
                await StopInternalAsync(true);
                return;
            }

            await PlayCurrentAsync(_perTrackLimitSeconds);
        }
        finally
        {
            _sessionLock.Release();
        }
    }

    void PlaybackSessionOnPlaybackStateChanged(MediaPlaybackSession sender, object args)
    {
        if (!settingsStore.Settings.EnableSmtc) return;
        var status = sender.PlaybackState switch
        {
            MediaPlaybackState.Playing => MediaPlaybackStatus.Playing,
            MediaPlaybackState.Paused => MediaPlaybackStatus.Paused,
            _ => MediaPlaybackStatus.Changing
        };
        smtcBridgeService.UpdatePlaybackStatus(_mediaPlayer, status);
    }

    async Task StopInternalAsync(bool raiseSessionEnded)
    {
        if (!IsPlaying && !raiseSessionEnded)
        {
            return;
        }

        _isStopping = true;
        _trackLimitCts?.Cancel();
        _sessionLimitCts?.Cancel();
        _trackLimitCts?.Dispose();
        _sessionLimitCts?.Dispose();
        _trackLimitCts = null;
        _sessionLimitCts = null;
        _mediaPlayer.Pause();
        _mediaPlayer.Source = null;
        smtcBridgeService.Clear(_mediaPlayer);
        _currentPlaylist = null;

        var shouldRaiseEnded = IsPlaying || raiseSessionEnded;
        IsPlaying = false;
        _queue.Clear();
        _index = -1;
        _isStopping = false;
        RaiseStateChanged();

        if (shouldRaiseEnded)
        {
            automationBridge.RaiseSessionEnded();
        }

        await Task.CompletedTask;
    }

    void RaiseStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);

    public void Dispose()
    {
        _trackLimitCts?.Cancel();
        _sessionLimitCts?.Cancel();
        _mediaPlayer.MediaEnded -= MediaPlayerOnMediaEnded;
        _mediaPlayer.MediaFailed -= MediaPlayerOnMediaFailed;
        _mediaPlayer.PlaybackSession.PlaybackStateChanged -= PlaybackSessionOnPlaybackStateChanged;
        _mediaPlayer.Dispose();
    }
}
