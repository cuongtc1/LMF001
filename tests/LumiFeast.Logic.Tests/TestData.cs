using LumiFeast.Logic.Data;

namespace LumiFeast.Logic.Tests;

/// <summary>Reads the real data/ folder, so a broken balance file fails the tests.</summary>
public sealed class DiskDataSource : IDataSource
{
    public static readonly string Root = FindRoot();

    public string ReadText(string path) => File.ReadAllText(Path.Combine(Root, path));

    public IEnumerable<string> ListFiles(string folder, string extension) =>
        Directory.EnumerateFiles(Path.Combine(Root, folder), "*" + extension, SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(Root, path).Replace('\\', '/'));

    private static string FindRoot()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(folder!.FullName, "project.godot")))
        {
            folder = folder.Parent;
        }

        return folder.FullName;
    }
}

/// <summary>The real data, shared by the tests.</summary>
public static class TestData
{
    public static readonly GameData Game = new GameDataLoader(new DiskDataSource()).Load();
}
