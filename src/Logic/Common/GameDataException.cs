namespace LumiFeast.Logic.Common;

/// <summary>Bad or missing game data. Carries a message key so the text stays in messages.properties.</summary>
public sealed class GameDataException(string messageKey, params object[] args) : Exception(messageKey)
{
    public string MessageKey { get; } = messageKey;

    public object[] MessageArgs { get; } = args;
}
