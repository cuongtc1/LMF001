using Godot;
using LumiFeast.Game.Common;
using LumiFeast.Logic.Common;

namespace LumiFeast.Game.Boot;

/// <summary>Entry scene: loads the data, then opens the first stage.</summary>
public partial class Boot : Node
{
    public override void _Ready()
    {
        try
        {
            GameServices.Load();
        }
        catch (GameDataException error)
        {
            GameLog.Error(error);
            GetTree().Quit(1);
            return;
        }

        var data = GameServices.Data;
        GameLog.Info(MessageKeys.DataLoaded, data.Characters.Count, data.Monsters.Count, data.Stages.Count);
        GameLog.Info(MessageKeys.GameBooted);
        GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, GameConstants.PrototypeScene);
    }
}
