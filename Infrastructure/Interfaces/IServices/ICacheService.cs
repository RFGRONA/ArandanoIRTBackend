namespace ArandanoIRT_Backend.Infrastructure.Interfaces.IServices
{
    /// <summary>
    /// Defines a contract for a generic caching service.
    /// Provides asynchronous operations to get, set (with expiration), and remove cached data items and collections.
    /// </summary>
    public interface ICacheService
    {
        /// <summary>
        /// Retrieves a cached item by its key.
        /// </summary>
        /// <typeparam name="T">The expected type of the cached item.</typeparam>
        /// <param name="key">The unique key identifying the cached item.</param>
        /// <returns>
        /// The cached item of type <typeparamref name="T"/> if found and not expired;
        /// otherwise, the default value for type <typeparamref name="T"/> (typically <c>null</c> for reference types).
        /// </returns>
        T? Get<T>(string key);

        /// <summary>
        /// Retrieves a cached collection of items by its key.
        /// </summary>
        /// <typeparam name="T">The expected type of items in the cached collection.</typeparam>
        /// <param name="key">The unique key identifying the cached collection.</param>
        /// <returns>
        /// An <see cref="IEnumerable{T}"/> containing the cached collection if found and not expired;
        /// otherwise, <c>null</c> (or potentially an empty enumerable depending on implementation).
        /// </returns>
        IEnumerable<T>? GetCollection<T>(string key);

        /// <summary>
        /// Adds or updates an item in the cache with a specified key and relative expiration time.
        /// If an item with the same key already exists, it is overwritten.
        /// </summary>
        /// <typeparam name="T">The type of the item to cache.</typeparam>
        /// <param name="key">The unique key to associate with the cached item.</param>
        /// <param name="value">The item to be stored in the cache.</param>
        /// <param name="expirationTime">The duration for which the item should remain valid in the cache, relative to the time it's set.</param>
        void Set<T>(string key, T value, TimeSpan expirationTime);

        /// <summary>
        /// Adds or updates a collection of items in the cache with a specified key and relative expiration time.
        /// If a collection with the same key already exists, it is overwritten.
        /// </summary>
        /// <typeparam name="T">The type of items in the collection to cache.</typeparam>
        /// <param name="key">The unique key to associate with the cached collection.</param>
        /// <param name="value">The collection of items to be stored in the cache.</param>
        /// <param name="expirationTime">The duration for which the collection should remain valid in the cache, relative to the time it's set.</param>
        void SetCollection<T>(string key, IEnumerable<T> value, TimeSpan expirationTime);

        /// <summary>
        /// Removes a cached item or collection identified by the specified key from the cache.
        /// If the key does not exist, the operation typically completes without error.
        /// </summary>
        /// <param name="key">The unique key of the item or collection to remove.</param>
        void Remove(string key);
    }
}