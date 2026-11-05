using System.Globalization;

namespace LumiFeast.Logic.Common;

/// <summary>Reads a Java-style .properties file. An unknown key returns !key! instead of crashing.</summary>
public sealed class MessageCatalog
{
    private readonly Dictionary<string, string> _messages = [];

    public static MessageCatalog Parse(string text)
    {
        var catalog = new MessageCatalog();
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line[0] is '#' or '!')
            {
                continue;
            }

            var separator = line.IndexOf('=');
            if (separator > 0)
            {
                catalog._messages[line[..separator].Trim()] = line[(separator + 1)..].Trim();
            }
        }

        return catalog;
    }

    public bool Contains(string key) => _messages.ContainsKey(key);

    public string Get(string key, params object[] args)
    {
        if (!_messages.TryGetValue(key, out var template))
        {
            return $"!{key}!";
        }

        return args.Length == 0 ? template : string.Format(CultureInfo.InvariantCulture, template, args);
    }
}
