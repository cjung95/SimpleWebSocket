// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using Jung.SimpleWebSocket.Models.EventArguments;
using Microsoft.Extensions.Logging;

namespace Jung.SimpleWebSocket.Utility;

/// <summary>
/// Creates a dictionary with expiration time for each item.
/// </summary>
/// <typeparam name="TKey">The type of the keys in the dictionary.</typeparam>
/// <typeparam name="TValue">The type of the values in the dictionary.</typeparam>
/// <param name="expiration">The expiration time for each item.</param>
/// <param name="logger">The logger to log exceptions.</param>
public class ExpiringDictionary<TKey, TValue>(TimeSpan expiration, ILogger? logger = null) where TKey : class
{

    /// <summary>
    /// Occurs when an item is expired.
    /// </summary>
    public event EventHandler<ItemExpiredArgs<TValue>>? ItemExpired;

    private readonly SortedList<DateTime, TKey> _expirationQueue = [];
    private readonly Dictionary<TKey, TValue> _dictionary = [];

    private bool _cleanupInProgress = false;

    /// <summary>
    /// Add the specified key and value to the dictionary.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    public void Add(TKey key, TValue value)
    {
        lock (_dictionary)
        {
            // Add the item to the dictionary
            _dictionary[key] = value;

            // Add item with expiration time to the queue
            var expirationTime = DateTime.Now.Add(expiration);

            lock (_expirationQueue)
            {
                _expirationQueue.Add(expirationTime, key);
            }
        }

        // Trigger cleanup after the Add operation is done
        lock (_expirationQueue)
        {
            if (!_cleanupInProgress)
            {
                _cleanupInProgress = true;

                // Run cleanup asynchronously
                CleanupExpiredItems().ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        // Handle exceptions here
                        logger?.LogError(t.Exception, "An Exception occurred during cleanup expired items.");
                    }
                });
            }
        }
    }

    /// <summary>
    /// Determines whether the dictionary contains the specified key.
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public bool ContainsKey(TKey key)
    {
        lock (_dictionary)
        {
            return _dictionary.ContainsKey(key);
        }
    }
    /// <summary>
    /// Removes the value with the specified key from the dictionary.
    /// </summary>
    /// <param name="key">The key of the value to remove.</param>
    /// <returns>Returns true if the element is successfully found and removed; otherwise, false.</returns>
    public bool Remove(TKey key)
    {
        lock (_dictionary)
        {
            if (_dictionary.Remove(key))
            {
                lock (_expirationQueue)
                {
                    // Find and remove the expiration time entry for this key
                    var expirationTime = _expirationQueue.FirstOrDefault(x => x.Value.Equals(key)).Key;
                    if (expirationTime != default)
                    {
                        _expirationQueue.Remove(expirationTime);
                    }
                }
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Get or set the value associated with the specified key.
    /// </summary>
    /// <param name="key">The key of the value to get or set.</param>
    /// <returns>The value associated with the specified key.</returns>
    public TValue this[TKey key]
    {
        get
        {
            lock (_dictionary)
            {
                return _dictionary[key];
            }
        }
        set
        {
            lock (_dictionary)
            {
                _dictionary[key] = value;
                var expirationTime = DateTime.Now.Add(expiration);
                lock (_expirationQueue)
                {
                    _expirationQueue[expirationTime] = key;
                }
            }
        }
    }

    /// <summary>
    /// Cleans up expired items.
    /// </summary>
    /// <returns>A task that represents the asynchronous cleanup operation.</returns>
    private async Task CleanupExpiredItems()
    {
        while (true)
        {
            DateTime nearestExpiration;
            TKey expiredKey;

            // Safely lock and retrieve the first item to expire
            lock (_expirationQueue)
            {
                // If there are no items, stop the cleanup process
                if (_expirationQueue.Count == 0)
                {
                    _cleanupInProgress = false;
                    return;
                }

                // Get the first key in expiration queue (FIFO order)
                var firstItem = _expirationQueue.First();
                nearestExpiration = firstItem.Key;
                expiredKey = firstItem.Value;
            }

            // Calculate the delay based on the expiration time
            TimeSpan delay = nearestExpiration - DateTime.Now;

            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay);
            }

            // Remove the expired item from the dictionary
            lock (_dictionary)
            {
                if (_dictionary.TryGetValue(expiredKey, out var expiredItem))
                {
                    ItemExpired?.Invoke(this, new ItemExpiredArgs<TValue>(expiredItem));
                    _dictionary.Remove(expiredKey);
                }
            }

            // Remove from expiration queue, but only if it's still the correct key
            lock (_expirationQueue)
            {
                if (_expirationQueue.Count > 0 && _expirationQueue.First().Value.Equals(expiredKey))
                {
                    _expirationQueue.RemoveAt(0); // Safely remove the correct item
                }
            }
        }
    }
}