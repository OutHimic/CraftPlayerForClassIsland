using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ClassIsland.Shared;
using CraftPlayer.Models;
using CraftPlayer.ViewModels;

namespace CraftPlayer.Views.SettingsPages;

[SettingsPageInfo("cn.craftine.craftplayer.settings", "CraftPlayer")]
public partial class CraftPlayerSettingsPage : SettingsPageBase
{
    readonly CraftPlayerSettingsViewModel _viewModel;
    readonly record struct TrackAnchorSnapshot(string TrackId, double RelativeY, Vector ScrollOffset);

    public CraftPlayerSettingsPage()
    {
        _viewModel = IAppHost.TryGetService<CraftPlayerSettingsViewModel>() ??
                     throw new InvalidOperationException("无法获取 CraftPlayerSettingsViewModel。");
        InitializeComponent();
        DataContext = _viewModel;
    }

    async void AddPlaylistButton_OnClick(object? sender, RoutedEventArgs e)
    {
        await _viewModel.AddPlaylistAsync(PlaylistNameTextBox.Text);
    }

    async void RenamePlaylistButton_OnClick(object? sender, RoutedEventArgs e)
    {
        await _viewModel.RenameSelectedPlaylistAsync(PlaylistNameTextBox.Text);
    }

    async void DeletePlaylistButton_OnClick(object? sender, RoutedEventArgs e)
    {
        await _viewModel.DeleteSelectedPlaylistAsync();
    }

    async void ImportButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择音频文件",
            AllowMultiple = true,
            FileTypeFilter =
            [
                new FilePickerFileType("音频文件")
                {
                    Patterns = ["*.wav", "*.mp3", "*.ogg", "*.flac"]
                }
            ]
        });

        var paths = files.Select(x => x.TryGetLocalPath()).Where(x => !string.IsNullOrWhiteSpace(x)).Cast<string>().ToList();
        await _viewModel.ImportFilesAsync(paths);
    }

    async void DeleteTrackButton_OnClick(object? sender, RoutedEventArgs e)
    {
        await _viewModel.DeleteSelectedTrackAsync();
    }

    async void ClearPlayedStateButton_OnClick(object? sender, RoutedEventArgs e)
    {
        await _viewModel.ClearPlayedStateAsync();
    }

    void CheckAllTracksButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _viewModel.CheckAllTracks();
    }

    void ClearCheckedTracksButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _viewModel.ClearTrackChecks();
    }

    async void MoveUpButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var anchor = CaptureTrackAnchor();
        await _viewModel.MoveSelectedTrackAsync(-1);
        RestoreTrackAnchor(anchor);
    }

    async void MoveDownButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var anchor = CaptureTrackAnchor();
        await _viewModel.MoveSelectedTrackAsync(1);
        RestoreTrackAnchor(anchor);
    }

    async void ShuffleButton_OnClick(object? sender, RoutedEventArgs e)
    {
        await _viewModel.ShuffleTracksAsync();
    }

    async void ExportCsvButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;
        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "导出歌单",
            SuggestedFileName = "playlist.csv",
            DefaultExtension = "csv",
            FileTypeChoices =
            [
                new FilePickerFileType("CSV")
                {
                    Patterns = ["*.csv"]
                }
            ]
        });

        if (file == null) return;
        var path = file.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path)) return;
        await _viewModel.ExportCurrentPlaylistCsvAsync(path);
    }

    void TrackDataGrid_OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (sender is not DataGrid trackDataGrid) return;
        trackDataGrid.MaxColumnWidth = Math.Max(120, e.NewSize.Width / 3);
    }

    TrackAnchorSnapshot? CaptureTrackAnchor()
    {
        var anchorTrack = _viewModel.Tracks.FirstOrDefault(x => x.IsChecked);
        if (anchorTrack == null) return null;

        var scrollViewer = TrackDataGrid.FindDescendantOfType<ScrollViewer>();
        if (scrollViewer == null) return new TrackAnchorSnapshot(anchorTrack.Id, 0, default);

        var row = FindTrackRow(anchorTrack);
        var relativeY = row?.TranslatePoint(default, scrollViewer)?.Y ?? 0;
        return new TrackAnchorSnapshot(anchorTrack.Id, relativeY, scrollViewer.Offset);
    }

    void RestoreTrackAnchor(TrackAnchorSnapshot? anchor)
    {
        if (anchor == null) return;
        Dispatcher.UIThread.Post(() =>
        {
            var anchorTrack = _viewModel.Tracks.FirstOrDefault(x => x.Id == anchor.Value.TrackId);
            if (anchorTrack == null) return;

            TrackDataGrid.ScrollIntoView(anchorTrack, null);
            Dispatcher.UIThread.Post(() =>
            {
                var scrollViewer = TrackDataGrid.FindDescendantOfType<ScrollViewer>();
                var row = FindTrackRow(anchorTrack);
                if (scrollViewer == null || row == null) return;

                var currentY = row.TranslatePoint(default, scrollViewer)?.Y;
                if (currentY == null) return;

                var delta = currentY.Value - anchor.Value.RelativeY;
                scrollViewer.Offset = new Vector(
                    anchor.Value.ScrollOffset.X,
                    Math.Max(0, scrollViewer.Offset.Y + delta));
            }, DispatcherPriority.Background);
        }, DispatcherPriority.Background);
    }

    DataGridRow? FindTrackRow(TrackItem track)
    {
        return TrackDataGrid
            .GetVisualDescendants()
            .OfType<DataGridRow>()
            .FirstOrDefault(x => ReferenceEquals(x.DataContext, track));
    }
}
