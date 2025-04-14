using ArandanoIRT_Backend.Infrastructure.Interfaces.IServices;
using Microsoft.Extensions.Caching.Memory;

namespace ArandanoIRT_Backend.Infrastructure.Services
{
    /// <summary>
    /// Implements the <see cref="ICacheService"/> interface using the built-in
    /// <see cref="IMemoryCache"/> for in-memory caching capabilities.
    /// </summary>
    /// <param name="memoryCache">The <see cref="IMemoryCache"/> instance provided by dependency injection.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="memoryCache"/> is null.</exception>
    public class CacheService(IMemoryCache memoryCache) : ICacheService
    {
        // Private field holding the injected IMemoryCache instance.
        private readonly IMemoryCache _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));

        /// <inheritdoc/>
        public T? Get<T>(string key)
        {
            // Retrieves the item from the underlying IMemoryCache.
            // Returns default(T) (which is null for reference types) if the key is not found or the item is expired.
            return _memoryCache.Get<T>(key);
        }

        /// <inheritdoc/>
        public IEnumerable<T>? GetCollection<T>(string key)
        {
            // Retrieves the collection from the underlying IMemoryCache.
            // Explicitly returns null if the key is not found or the item is expired.
            return _memoryCache.Get<IEnumerable<T>>(key) ?? null;
        }

        /// <inheritdoc/>
        public void Set<T>(string key, T value, TimeSpan expirationTime)
        {
            // Sets the item in the underlying IMemoryCache with a relative expiration time.
            _memoryCache.Set(key, value, expirationTime);
        }

        /// <inheritdoc/>
        public void SetCollection<T>(string key, IEnumerable<T> value, TimeSpan expirationTime)
        {
            // Converts the IEnumerable to a List before caching.
            // This ensures that any deferred execution (like from LINQ queries) is resolved
            // before storing the collection in the cache.
            _memoryCache.Set(key, value.ToList(), expirationTime);
        }

        /// <inheritdoc/>
        public void Remove(string key)
        {
            // Removes the item associated with the key from the underlying IMemoryCache.
            _memoryCache.Remove(key);
        }
    }
}