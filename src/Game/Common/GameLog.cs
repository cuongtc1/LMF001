using Godot;
using LumiFeast.Logic.Common;

namespace LumiFeast.Game.Common;

/// <summary>All logging goes through message keys; the text lives in messages.properties.</summary>
public static class GameLog
{
    public static void Info(string key, params object[] args) => GD.Print(GameServices.Messages.Get(key, args));

    public static void Error(string key, params object[] args) => GD.PushError(GameServices.Messages.Get(key, args));

    public static void Error(GameDataException error) => Error(error.MessageKey, error.MessageArgs);
}
