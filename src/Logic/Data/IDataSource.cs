namespace LumiFeast.Logic.Data;

/// <summary>Where data files come from: Godot res:// in the game, the disk or memory in tests.</summary>
public interface IDataSource
{
    string ReadText(string path);

    /// <summary>Files under folder and its subfolders, as paths ReadText accepts.</summary>
    IEnumerable<string> ListFiles(string folder, string extension);
}
