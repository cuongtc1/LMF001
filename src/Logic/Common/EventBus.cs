namespace LumiFeast.Logic.Common;

/// <summary>Typed publish/subscribe between systems, so they do not reference each other.</summary>
public sealed class EventBus
{
    private readonly Dictionary<Type, List<Delegate>> _handlers = [];

    public void Subscribe<T>(Action<T> handler)
    {
        if (!_handlers.TryGetValue(typeof(T), out var list))
        {
            _handlers[typeof(T)] = list = [];
        }

        list.Add(handler);
    }

    public void Unsubscribe<T>(Action<T> handler)
    {
        if (_handlers.TryGetValue(typeof(T), out var list))
        {
            list.Remove(handler);
        }
    }

    public void Publish<T>(T message)
    {
        if (!_handlers.TryGetValue(typeof(T), out var list))
        {
            return;
        }

        // Copy: a handler may unsubscribe while this runs.
        foreach (var handler in list.ToArray())
        {
            ((Action<T>)handler)(message);
        }
    }
}
