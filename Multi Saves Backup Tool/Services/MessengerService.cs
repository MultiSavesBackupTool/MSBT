using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Multi_Saves_Backup_Tool.Services;

public class MessengerService
{
    private static MessengerService? _instance;
    private static readonly Lock Lock = new();
    private readonly ILogger? _logger;

    private readonly ConcurrentDictionary<Type, List<Action<object>>> _subscribers = new();

    private MessengerService(ILogger? logger = null)
    {
        _logger = logger;
    }

    public static MessengerService Instance
    {
        get
        {
            if (_instance == null)
                lock (Lock)
                {
                    _instance ??= new MessengerService();
                }

            return _instance;
        }
    }

    public void Send<T>(T message)
    {
        try
        {
            if (message == null)
            {
                _logger?.LogWarning("Attempted to send null message of type {MessageType}", typeof(T).Name);
                return;
            }

            var messageType = typeof(T);
            if (_subscribers.TryGetValue(messageType, out var handlers))
            {
                var handlersCopy = handlers.ToArray();
                foreach (var handler in handlersCopy)
                    try
                    {
                        handler(message);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Error in message handler for type {MessageType}", messageType.Name);
                    }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error sending message of type {MessageType}", typeof(T).Name);
        }
    }

    public void Subscribe<T>(Action<T> handler) where T : class
    {
        if (handler == null) throw new ArgumentNullException(nameof(handler));

        try
        {
            var messageType = typeof(T);
            var handlers = _subscribers.GetOrAdd(messageType, _ => new List<Action<object>>());

            lock (handlers)
            {
                handlers.Add(obj => handler((T)obj));
            }

            _logger?.LogDebug("Subscribed to message type {MessageType}", messageType.Name);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error subscribing to message type {MessageType}", typeof(T).Name);
            throw;
        }
    }

    public void Unsubscribe<T>(Action<T> handler) where T : class
    {
        if (handler == null) throw new ArgumentNullException(nameof(handler));

        try
        {
            var messageType = typeof(T);
            if (_subscribers.TryGetValue(messageType, out var handlers))
            {
                var removedCount = handlers.RemoveAll(h => h.Method == handler.Method && h.Target == handler.Target);
                _logger?.LogDebug("Unsubscribed {Count} handlers from message type {MessageType}", removedCount,
                    messageType.Name);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error unsubscribing from message type {MessageType}", typeof(T).Name);
            throw;
        }
    }

    public void Clear()
    {
        try
        {
            _subscribers.Clear();
            _logger?.LogInformation("Cleared all message subscriptions");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error clearing message subscriptions");
        }
    }

    public int GetSubscriptionCount<T>() where T : class
    {
        var messageType = typeof(T);
        if (_subscribers.TryGetValue(messageType, out var handlers))
            lock (handlers)
            {
                return handlers.Count;
            }

        return 0;
    }

    public bool HasSubscribers<T>() where T : class
    {
        return GetSubscriptionCount<T>() > 0;
    }
}