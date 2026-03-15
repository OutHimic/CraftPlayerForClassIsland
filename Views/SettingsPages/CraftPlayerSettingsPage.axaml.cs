using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ClassIsland.Shared;
using CraftPlayer.ViewModels;

namespace CraftPlayer.Views.SettingsPages;

[SettingsPageInfo("cn.craftine.craftplayer.settings", "CraftPlayer")]
public partial class CraftPlayerSettingsPage : SettingsPageBase
{
    readonly CraftPlayerSettingsViewModel _viewModel;

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

    async void LockPlaylistButton_OnClick(object? sender, RoutedEventArgs e)
    {
        await _viewModel.LockSelectedPlaylistAsync();
    }

    async void UnlockPlaylistButton_OnClick(object? sender, RoutedEventArgs e)
    {
        await _viewModel.UnlockSelectedPlaylistAsync();
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

    async void MoveUpButton_OnClick(object? sender, RoutedEventArgs e)
    {
        await _viewModel.MoveSelectedTrackAsync(-1);
    }

    async void MoveDownButton_OnClick(object? sender, RoutedEventArgs e)
    {
        await _viewModel.MoveSelectedTrackAsync(1);
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
}
