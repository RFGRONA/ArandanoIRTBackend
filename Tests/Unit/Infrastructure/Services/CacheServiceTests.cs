using ArandanoIRT_Backend.Infrastructure.Interfaces.IServices;
using ArandanoIRT_Backend.Infrastructure.Services; 
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Xunit;

namespace ArandanoIRT_Backend.Tests.Unit.Infrastructure.Services
{
    /// <summary>
    /// Contains unit tests for the <see cref="CacheService"/> class.
    /// Uses a real <see cref="MemoryCache"/> instance for testing cache interactions.
    /// Implements <see cref="IDisposable"/> to ensure the cache is disposed after tests.
    /// </summary>
    public class CacheServiceTests : IDisposable
    {
        /// <summary> The underlying MemoryCache instance used for tests. </summary>
        private readonly MemoryCache _memoryCache;
        /// <summary> The CacheService instance under test. </summary>
        private readonly CacheService _cacheService;
        /// <summary> A default short expiration duration for most cache tests. </summary>
        private readonly TimeSpan _defaultExpiration = TimeSpan.FromMilliseconds(200);
        /// <summary> A very short expiration duration for testing item expiry. </summary>
        private readonly TimeSpan _veryShortExpiration = TimeSpan.FromMilliseconds(50);

        /// <summary>
        /// Defines a simple record type for storing test data in the cache.
        /// </summary>
        private record TestData(int Id, string Name);

        /// <summary>
        /// Initializes a new instance of the <see cref="CacheServiceTests"/> class.
        /// Sets up the real <see cref="MemoryCache"/> instance and the <see cref="CacheService"/>.
        /// </summary>
        public CacheServiceTests()
        {
            // Creates MemoryCacheOptions (optional, defaults can be used).
            var options = Options.Create(new MemoryCacheOptions());
            _memoryCache = new MemoryCache(options);
            // Injects the real MemoryCache instance into the service under test.
            _cacheService = new CacheService(_memoryCache);
        }

        /// <summary>
        /// Cleans up the <see cref="MemoryCache"/> instance after each test run
        /// by calling its Dispose method.
        /// </summary>
        public void Dispose()
        {
            _memoryCache?.Dispose();
            GC.SuppressFinalize(this);
        }

        // --- Tests for Get<T> ---

        [Fact]
        public void Get_WhenItemExistsAndNotExpired_ShouldReturnItem()
        {
            // Arrange
            var key = "existing_item";
            var expectedItem = new TestData(1, "Test Item");
            _cacheService.Set(key, expectedItem, _defaultExpiration);

            // Act
            var result = _cacheService.Get<TestData>(key);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEquivalentTo(expectedItem);
        }

        [Fact]
        public void Get_WhenItemDoesNotExist_ShouldReturnNull()
        {
            // Arrange
            var key = "non_existent_item";

            // Act
            var result = _cacheService.Get<TestData>(key);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task Get_WhenItemExistsButExpired_ShouldReturnNull()
        {
            // Arrange
            var key = "expired_item";
            var item = new TestData(2, "Expired Item");
            // Sets the item with a very short expiration time.
            _cacheService.Set(key, item, _veryShortExpiration);

            // Act
            // Waits for a duration longer than the expiration time.
            await Task.Delay(_defaultExpiration);
            var result = _cacheService.Get<TestData>(key);

            // Assert
            result.Should().BeNull(); // Item should have been evicted due to expiration.
        }

        // --- Tests for GetCollection<T> ---

        [Fact]
        public void GetCollection_WhenCollectionExistsAndNotExpired_ShouldReturnCollection()
        {
            // Arrange
            var key = "existing_collection";
            var expectedCollection = new List<TestData>
            {
                new TestData(10, "Item 1"),
                new TestData(11, "Item 2")
            };
            _cacheService.SetCollection(key, expectedCollection, _defaultExpiration);

            // Act
            var result = _cacheService.GetCollection<TestData>(key);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEquivalentTo(expectedCollection);
            // Verifies that the retrieved object is specifically a List<TestData>.
            result.Should().BeOfType<List<TestData>>();
        }

        [Fact]
        public void GetCollection_WhenCollectionDoesNotExist_ShouldReturnNull()
        {
            // Arrange
            var key = "non_existent_collection";

            // Act
            var result = _cacheService.GetCollection<TestData>(key);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetCollection_WhenCollectionExistsButExpired_ShouldReturnNull()
        {
            // Arrange
            var key = "expired_collection";
            var collection = new List<TestData> { new TestData(20, "Expired Coll Item") };
            _cacheService.SetCollection(key, collection, _veryShortExpiration);

            // Act
            // Waits for the cache item to expire.
            await Task.Delay(_defaultExpiration);
            var result = _cacheService.GetCollection<TestData>(key);

            // Assert
            result.Should().BeNull(); // Collection should have been evicted.
        }

        // --- Tests for Set<T> ---

        [Fact]
        public void Set_ShouldAddItemToCache_AndGetShouldRetrieveIt()
        {
            // Arrange
            var key = "set_item_test";
            var itemToSet = new TestData(3, "Set Item");

            // Act
            _cacheService.Set(key, itemToSet, _defaultExpiration);
            var retrievedItem = _cacheService.Get<TestData>(key);

            // Assert
            retrievedItem.Should().NotBeNull();
            retrievedItem.Should().BeEquivalentTo(itemToSet);
        }

        // --- Tests for SetCollection<T> ---

        [Fact]
        public void SetCollection_ShouldAddCollectionToCache_AndGetShouldRetrieveIt()
        {
            // Arrange
            var key = "set_collection_test";
            var collectionToSet = new List<TestData> { new TestData(30, "Set Coll Item") };

            // Act
            _cacheService.SetCollection(key, collectionToSet, _defaultExpiration);
            var retrievedCollection = _cacheService.GetCollection<TestData>(key);

            // Assert
            retrievedCollection.Should().NotBeNull();
            retrievedCollection.Should().BeEquivalentTo(collectionToSet);
        }

        [Fact]
        public void SetCollection_WithIEnumerable_ShouldStoreAsList()
        {
            // Arrange
            var key = "ienumerable_test";
            // Uses an IEnumerable that isn't already a List (e.g., an array).
            var collectionToSet = new[] { new TestData(40, "Array Item") };

            // Act
            _cacheService.SetCollection(key, collectionToSet, _defaultExpiration);
            // Retrieves the collection (GetCollection returns IEnumerable<T>).
            var retrievedCollection = _cacheService.GetCollection<TestData>(key);

            // Assert using MemoryCache directly to check the actual stored type if necessary.
            var cacheEntry = _memoryCache.Get(key);
            cacheEntry.Should().NotBeNull();
            // Verifies that the service stored the data internally as a List<T>.
            cacheEntry.Should().BeOfType<List<TestData>>();
            // Verifies the content of the retrieved collection.
            retrievedCollection.Should().BeEquivalentTo(collectionToSet);
        }


        // --- Tests for Remove ---

        [Fact]
        public void Remove_WhenItemExists_ShouldRemoveItemFromCache()
        {
            // Arrange
            var key = "remove_test_item";
            var item = new TestData(4, "Item to Remove");
            _cacheService.Set(key, item, _defaultExpiration);

            // Verifies the item exists in the cache before removal.
            var itemBeforeRemove = _cacheService.Get<TestData>(key);
            itemBeforeRemove.Should().NotBeNull();

            // Act
            _cacheService.Remove(key);
            var itemAfterRemove = _cacheService.Get<TestData>(key);

            // Assert
            itemAfterRemove.Should().BeNull(); // Item should no longer be in the cache.
        }

        [Fact]
        public void Remove_WhenItemDoesNotExist_ShouldNotThrowException()
        {
            // Arrange
            var key = "remove_non_existent";

            // Act
            Action act = () => _cacheService.Remove(key);

            // Assert
            act.Should().NotThrow(); // Removing a non-existent key should be safe.
        }
    }
}