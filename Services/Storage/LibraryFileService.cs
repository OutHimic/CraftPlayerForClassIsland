using CraftPlayer.Models;

namespace CraftPlayer.Services.Storage;

public class LibraryFileService(SettingsStore settingsStore)
{
    static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".wav", ".mp3", ".ogg", ".flac"
    };

    public bool IsSupportedAudio(string path)
    {
        var ext = Path.GetExtension(path);
        return SupportedExtensions.Contains(ext);
    }

    public string EnsurePlaylistFolder(string playlistId)
    {
        var dir = Path.Combine(settingsStore.LibraryFolder, playlistId);
        Directory.CreateDirectory(dir);
        return dir;
    }

    public string ImportToPlaylist(string sourceFilePath, string playlistId)
    {
        if (!File.Exists(sourceFilePath)) throw new FileNotFoundException("音频文件不存在。", sourceFilePath);
        if (!IsSupportedAudio(sourceFilePath)) throw new InvalidOperationException("不支持的音频格式。");

        var targetDir = EnsurePlaylistFolder(playlistId);
        var fileName = Path.GetFileName(sourceFilePath);
        var targetPath = Path.Combine(targetDir, fileName);

        if (File.Exists(targetPath))
        {
            var name = Path.GetFileNameWithoutExtension(fileName);
            var ext = Path.GetExtension(fileName);
            targetPath = Path.Combine(targetDir, $"{name}_{Guid.NewGuid():N}{ext}");
        }

        File.Copy(sourceFilePath, targetPath);
        return targetPath;
    }

    public string GetAbsolutePath(TrackItem track)
    {
        return Path.Combine(settingsStore.ConfigFolder, track.RelativePath);
    }

    public static string ToRelativePath(string basePath, string fullPath)
    {
        return Path.GetRelativePath(basePath, fullPath);
    }

    public void DeleteTrackFileIfExists(TrackItem track)
    {
        var fullPath = GetAbsolutePath(track);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
    }
}
