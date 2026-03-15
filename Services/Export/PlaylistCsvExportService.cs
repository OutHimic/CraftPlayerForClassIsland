using System.Text;
using CraftPlayer.Models;

namespace CraftPlayer.Services.Export;

public class PlaylistCsvExportService
{
    public async Task ExportAsync(Playlist playlist, string filePath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("文件名,标题,艺术家,时长,相对路径");

        foreach (var track in playlist.Tracks.OrderBy(x => x.SortIndex))
        {
            sb.AppendLine(
                $"{Escape(track.FileName)},{Escape(track.Title)},{Escape(track.Artist)},{Escape(track.Duration.ToString())},{Escape(track.RelativePath)}");
        }

        var encoding = new UTF8Encoding(true);
        await File.WriteAllTextAsync(filePath, sb.ToString(), encoding);
    }

    static string Escape(string? text)
    {
        var value = text ?? "";
        value = value.Replace("\"", "\"\"");
        return $"\"{value}\"";
    }
}
