using System.Collections.Generic;
using Godot;
using LumiFeast.Logic.Common;
using LumiFeast.Logic.Data;

namespace LumiFeast.Game.Common;

/// <summary>Reads data files through Godot, so they also work from an exported game.</summary>
public sealed class GodotDataSource : IDataSource
{
    public string ReadText(string path)
    {
        var fullPath = GameConstants.ResourceRoot + path;
        if (!FileAccess.FileExists(fullPath))
        {
            throw new GameDataException(MessageKeys.DataFileMissing, path);
        }

        return FileAccess.GetFileAsString(fullPath);
    }

    public IEnumerable<string> ListFiles(string folder, string extension)
    {
        using var directory = DirAccess.Open(GameConstants.ResourceRoot + folder)
            ?? throw new GameDataException(MessageKeys.DataFileMissing, folder);
        foreach (var file in directory.GetFiles())
        {
            if (file.EndsWith(extension, System.StringComparison.Ordinal))
            {
                yield return $"{folder}/{file}";
            }
        }

        foreach (var child in directory.GetDirectories())
        {
            foreach (var file in ListFiles($"{folder}/{child}", extension))
            {
                yield return file;
            }
        }
    }
}
