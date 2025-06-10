using System;
using System.Collections.Generic;

namespace Multi_Saves_Backup_Tool.Services;

public class MessengerService
{
    private static MessengerService? _instance;

    private readonly Dictionary<Type, List<Action<object>>> _subscribers = new();
    public static MessengerService Instance => _instance ??= new MessengerService();

    public void Send<T>(T message)
    {
        var messageType = typeof(T);
        if (_subscribers.TryGetValue(messageType, out var handlers))
            foreach (var handler in handlers)
                if (message != null)
                    handler(message);
    }

    public void Subscribe<T>(Action<T> handler) where T : class
    {
        var messageType = typeof(T);
        if (!_subscribers.ContainsKey(messageType)) _subscribers[messageType] = new List<Action<object>>();

        _subscribers[messageType].Add(obj => handler((T)obj));
    }

    public void Unsubscribe<T>(Action<T> handler) where T : class
    {
        var messageType = typeof(T);
        if (_subscribers.TryGetValue(messageType, out var handlers))
            handlers.RemoveAll(h => h.Method == handler.Method && h.Target == handler.Target);
    }
}