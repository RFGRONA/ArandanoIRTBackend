using ArandanoIRT_Backend.Application.Interfaces.Auditing;
using ArandanoIRT_Backend.Infrastructure.Persistence.Auditing; 
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Logging.Abstractions;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Xunit;

namespace ArandanoIRT_Backend.Tests.Unit.Infrastructure.Persistence
{
    /// <summary>
    /// Contains unit tests for the <see cref="AuditHelperService"/> class.
    /// Uses an InMemory DbContext to simulate Entity Framework Core behavior.
    /// </summary>
    public class AuditHelperServiceTests : IDisposable
    {
        // --- Test Entity Definitions ---

        /// <summary>
        /// Test entity with a simple integer primary key and a nullable CropId.
        /// Includes a 'Password' property for testing exclusion.
        /// </summary>
        private class TestEntitySimplePk
        {
            [Key]
            public int Id { get; set; }
            public string Name { get; set; } = "Test Name";
            // Nullable CropId for testing GetCropIdValue.
            public int? CropId { get; set; }
            // Property intended to be excluded from audit logs.
            public string Password { get; set; } = "Secret";
            public DateTime Timestamp { get; set; } = DateTime.UtcNow;
            public bool IsActive { get; set; } = true;
        }

        /// <summary>
        /// Test entity with a composite primary key.
        /// </summary>
        private class TestEntityCompositePk
        {
            // Requires configuration in OnModelCreating.
            [Key]
            public int IdPart1 { get; set; }
            [Key]
            public string IdPart2 { get; set; } = "Part2";
            public string Data { get; set; } = "Some data";
            public int? CropId { get; set; }
        }

        /// <summary>
        /// Test entity that does not have a CropId property.
        /// </summary>
        private class TestEntityNoCropId
        {
            [Key]
            public int Id { get; set; }
            public string Value { get; set; } = "No Crop Here";
        }

        /// <summary>
        /// Test entity with a string primary key.
        /// </summary>
        private class TestEntityStringPk
        {
            [Key]
            public string Id { get; set; } = "key-1";
            public string Data { get; set; } = "Data String PK";
            public int? CropId { get; set; }
        }


        // --- Test DbContext ---

        /// <summary>
        /// Simple DbContext using InMemory provider for testing purposes.
        /// Includes DbSets for various test entity types.
        /// </summary>
        private class TestDbContext : DbContext
        {
            public DbSet<TestEntitySimplePk> SimpleEntities { get; set; }
            public DbSet<TestEntityCompositePk> CompositeEntities { get; set; }
            public DbSet<TestEntityNoCropId> NoCropIdEntities { get; set; }
            public DbSet<TestEntityStringPk> StringPkEntities { get; set; }

            public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                // Configures the composite primary key for TestEntityCompositePk.
                modelBuilder.Entity<TestEntityCompositePk>().HasKey(e => new { e.IdPart1, e.IdPart2 });
                base.OnModelCreating(modelBuilder);
            }
        }

        // --- Test Setup ---
        /// <summary> Options for configuring the InMemory DbContext. </summary>
        private readonly DbContextOptions<TestDbContext> _dbContextOptions;
        /// <summary> The DbContext instance used for testing. </summary>
        private TestDbContext _context;
        /// <summary> The instance of the AuditHelperService under test. </summary>
        private readonly AuditHelperService _auditHelperService;

        /// <summary>
        /// Initializes a new instance of the <see cref="AuditHelperServiceTests"/> class,
        /// setting up the InMemory database context and the service under test.
        /// </summary>
        public AuditHelperServiceTests()
        {
            // Configures the InMemory database options with a unique name per test run
            // to ensure test isolation.
            _dbContextOptions = new DbContextOptionsBuilder<TestDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            // Creates a new DbContext instance for each test.
            _context = new TestDbContext(_dbContextOptions);

            // Instantiates the service under test with a null logger.
            _auditHelperService = new AuditHelperService(NullLogger<AuditHelperService>.Instance);
        }

        /// <summary>
        /// Gets an EntityEntry for a given entity and simulates a specific EntityState.
        /// Handles attaching/adding the entity and setting OriginalValues for Modified/Deleted states.
        /// </summary>
        /// <typeparam name="TEntity">The type of the entity.</typeparam>
        /// <param name="entity">The entity instance.</param>
        /// <param name="state">The desired EntityState to simulate.</param>
        /// <returns>The EntityEntry tracking the entity with the specified state.</returns>
        private EntityEntry GetTrackedEntry<TEntity>(TEntity entity, EntityState state) where TEntity : class
        {
            // Ensure the entity is detached first if already tracked.
            var existingEntry = _context.Entry(entity);
            if (existingEntry.State != EntityState.Detached)
            {
                existingEntry.State = EntityState.Detached;
            }

            // Declare the entry variable.
            EntityEntry entry;

            if (state == EntityState.Added)
            {
                // Adding the entity sets its state to Added and returns the entry.
                entry = _context.Add(entity);
            }
            else
            {
                // For Modified/Deleted states, we need to manage OriginalValues carefully.
                // Creates a simple deep copy via JSON serialization to represent the original state.
                // Note: This approach has limitations for complex types or circular references.
                var originalEntity = JsonSerializer.Deserialize<TEntity>(JsonSerializer.Serialize(entity));

                // Attaches the *current* state of the entity to the context.
                _context.Attach(entity);
                // Gets the EntityEntry for the attached entity.
                entry = _context.Entry(entity);

                // Sets the OriginalValues based on the deep copy *before* changing the state.
                if (originalEntity != null)
                {
                    entry.OriginalValues.SetValues(originalEntity);
                }

                // Sets the desired state (Modified or Deleted) *after* OriginalValues are set.
                entry.State = state;

                // If testing Modified state, simulates an actual property change *after* attaching
                // and setting original values, ensuring the change tracker detects it.
                if (state == EntityState.Modified && entity is TestEntitySimplePk simple)
                {
                    simple.Name = "Modified Name"; // Example modification
                    entry.Property(nameof(TestEntitySimplePk.Name)).IsModified = true; // Mark as modified
                }
            }
            // Returns the configured EntityEntry.
            return entry;
        }

        /// <summary>
        /// Disposes the DbContext instance after each test run.
        /// </summary>
        public void Dispose()
        {
            _context?.Dispose();
            GC.SuppressFinalize(this);
        }


        // --- Tests for GetPrimaryKeyValue ---

        [Fact]
        public void GetPrimaryKeyValue_ForAddedEntity_ShouldReturnNull()
        {
            // Arrange
            // Simulates an entity being added, where the PK might be 0 or not yet assigned by the DB.
            var entity = new TestEntitySimplePk { Id = 0 };
            var entry = GetTrackedEntry(entity, EntityState.Added);

            // Act
            var result = _auditHelperService.GetPrimaryKeyValue(entry);

            // Assert
            // The service is expected to return null for added entities or non-integer PKs.
            result.Should().BeNull();
        }

        [Fact]
        public void GetPrimaryKeyValue_ForModifiedEntity_ShouldReturnCorrectPk()
        {
            // Arrange
            var entity = new TestEntitySimplePk { Id = 123, CropId = 10 };
            // Simulates fetching, modifying, and saving an entity.
            var entry = GetTrackedEntry(entity, EntityState.Modified);
            // Simulate modification after tracking.
            entity.Name = "Updated Name";

            // Act
            var result = _auditHelperService.GetPrimaryKeyValue(entry);

            // Assert
            result.Should().Be(123); // Should return the existing PK value.
        }

        [Fact]
        public void GetPrimaryKeyValue_ForDeletedEntity_ShouldReturnCorrectPk()
        {
            // Arrange
            var entity = new TestEntitySimplePk { Id = 456 };
            // Simulates fetching and marking an entity for deletion.
            var entry = GetTrackedEntry(entity, EntityState.Deleted);

            // Act
            var result = _auditHelperService.GetPrimaryKeyValue(entry);

            // Assert
            result.Should().Be(456); // Should return the PK value of the entity being deleted.
        }

        [Fact]
        public void GetPrimaryKeyValue_ForCompositePkEntity_ShouldReturnNull()
        {
            // Arrange
            var entity = new TestEntityCompositePk { IdPart1 = 1, IdPart2 = "A" };
            var entry = GetTrackedEntry(entity, EntityState.Modified);

            // Act
            var result = _auditHelperService.GetPrimaryKeyValue(entry);

            // Assert
            // Service returns null for composite keys.
            result.Should().BeNull();
        }

        [Fact]
        public void GetPrimaryKeyValue_ForStringPkEntity_ShouldReturnNull()
        {
            // Arrange
            var entity = new TestEntityStringPk { Id = "abc-123" };
            var entry = GetTrackedEntry(entity, EntityState.Modified);

            // Act
            var result = _auditHelperService.GetPrimaryKeyValue(entry);

            // Assert
            // Service returns null for non-integer single keys.
            result.Should().BeNull();
        }

        // --- Tests for GetCropIdValue ---

        [Fact]
        public void GetCropIdValue_ForEntityWithCropId_ShouldReturnCropId()
        {
            // Arrange
            var entity = new TestEntitySimplePk { Id = 789, CropId = 55 };
            var entry = GetTrackedEntry(entity, EntityState.Modified);
            // Simulate modification after tracking.
            entity.Name = "Updated Name";

            // Act
            var result = _auditHelperService.GetCropIdValue(entry);

            // Assert
            result.Should().Be(55); // Expects the CropId value.
        }

        [Fact]
        public void GetCropIdValue_ForDeletedEntityWithCropId_ShouldReturnCropId()
        {
            // Arrange
            var entity = new TestEntitySimplePk { Id = 790, CropId = 66 };
            var entry = GetTrackedEntry(entity, EntityState.Deleted);

            // Act
            var result = _auditHelperService.GetCropIdValue(entry);

            // Assert
            result.Should().Be(66); // Should get CropId even from a deleted entity's original values.
        }

        [Fact]
        public void GetCropIdValue_ForEntityWithNullCropId_ShouldReturnNull()
        {
            // Arrange
            var entity = new TestEntitySimplePk { Id = 800, CropId = null };
            var entry = GetTrackedEntry(entity, EntityState.Modified);

            // Act
            var result = _auditHelperService.GetCropIdValue(entry);

            // Assert
            result.Should().BeNull(); // Expects null if CropId is null.
        }

        [Fact]
        public void GetCropIdValue_ForEntityWithoutCropIdProperty_ShouldReturnNull()
        {
            // Arrange
            var entity = new TestEntityNoCropId { Id = 900 };
            var entry = GetTrackedEntry(entity, EntityState.Modified);

            // Act
            var result = _auditHelperService.GetCropIdValue(entry);

            // Assert
            result.Should().BeNull(); // Expects null if the property doesn't exist.
        }

        // --- Tests for GetValuesDictionary ---

        [Fact]
        public void GetValuesDictionary_WithCurrentValues_ShouldReturnDictionaryWithoutExcluded()
        {
            // Arrange
            var entity = new TestEntitySimplePk { Id = 1000, CropId = 77, Password = "ShouldBeExcluded" };
            // Ensures the entity is tracked to have CurrentValues populated.
            var entry = GetTrackedEntry(entity, EntityState.Modified);
            // Makes a change to ensure CurrentValues reflect the latest state.
            entity.Name = "New Name";

            // Act
            var dictionary = _auditHelperService.GetValuesDictionary(entry.CurrentValues);

            // Assert
            dictionary.Should().NotBeNull();
            dictionary!.Should().ContainKey("Id").WhoseValue.Should().Be(1000);
            dictionary.Should().ContainKey("CropId").WhoseValue.Should().Be(77);
            dictionary.Should().ContainKey("Name").WhoseValue.Should().Be("New Name"); // Reflects modified value
            // Checks that other properties exist.
            dictionary.Should().ContainKey("Timestamp");
            dictionary.Should().ContainKey("IsActive");
            // Verifies that the 'Password' property was excluded by default.
            dictionary.Should().NotContainKey("Password");
        }

        [Fact]
        public void GetValuesDictionary_WithOriginalValues_ShouldReturnDictionaryWithoutExcluded()
        {
            // Arrange
            var entity = new TestEntitySimplePk { Id = 1001, CropId = 88, Name = "Original Name" };
            // Sets OriginalValues in the helper method when state is Modified/Deleted.
            var entry = GetTrackedEntry(entity, EntityState.Modified);
            // Perform the actual modification after tracking setup.
            entity.Name = "Modified Name";

            // Act
            var dictionary = _auditHelperService.GetValuesDictionary(entry.OriginalValues);

            // Assert
            dictionary.Should().NotBeNull();
            // Checks that a key exists.
            dictionary!.Should().ContainKey("CropId");
            // Checks the value for that specific key.
            dictionary["CropId"].Should().Be(88);
            // Checks the original value of the 'Name' property.
            dictionary.Should().ContainKey("Name").WhoseValue.Should().Be("Original Name");
            dictionary.Should().NotContainKey("Password"); // Verifies default exclusion
        }

        [Fact]
        public void GetValuesDictionary_WithAdditionalExclusions_ShouldExcludeMoreProperties()
        {
            // Arrange
            var entity = new TestEntitySimplePk { Id = 1002, Name = "Keep", Timestamp = DateTime.UtcNow, CropId = 99 }; // Added CropId here
            var entry = GetTrackedEntry(entity, EntityState.Modified);
            // Specifies additional properties to exclude.
            var additionalExclusions = new List<string> { "Timestamp", "IsActive" };

            // Act
            var dictionary = _auditHelperService.GetValuesDictionary(entry.CurrentValues, additionalExclusions);

            // Assert
            dictionary.Should().NotBeNull();
            dictionary!.Should().ContainKey("Id").WhoseValue.Should().Be(1002);
            // Name reflects the modified value due to test setup.
            dictionary.Should().ContainKey("Name").WhoseValue.Should().Be("Modified Name"); // Updated expected value
            // Verifies default exclusion.
            dictionary.Should().NotContainKey("Password");
            // Verifies additional exclusion.
            dictionary.Should().NotContainKey("Timestamp");
            // Verifies additional exclusion.
            dictionary.Should().NotContainKey("IsActive");
            // Verifies that CropId is still included.
            dictionary.Should().ContainKey("CropId").WhoseValue.Should().Be(99);
        }

        [Fact]
        public void GetValuesDictionary_WithNullPropertyValues_ShouldReturnNull()
        {
            // Act
            var dictionary = _auditHelperService.GetValuesDictionary(null);

            // Assert
            dictionary.Should().BeNull(); // Expects null when input PropertyValues is null.
        }

        // --- Tests for SerializePropertyValueDictionary ---

        [Fact]
        public void SerializePropertyValueDictionary_WithValidDictionary_ShouldReturnJsonString()
        {
            // Arrange
            var dict = new Dictionary<string, object?>
            {
                { "Id", 1 },
                { "Name", "Test" },
                { "CropId", 5 },
                { "IsActive", true },
                { "NullableInt", null },
                { "Date", new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc) }
            };

            // Act
            var jsonResult = _auditHelperService.SerializePropertyValueDictionary(dict);

            // Assert
            jsonResult.Should().NotBeNullOrWhiteSpace();
            // Performs basic checks for JSON structure and content accuracy.
            jsonResult.Should().StartWith("{");
            jsonResult.Should().EndWith("}");
            jsonResult.Should().Contain("\"Id\":1");
            jsonResult.Should().Contain("\"Name\":\"Test\"");
            jsonResult.Should().Contain("\"CropId\":5");
            jsonResult.Should().Contain("\"IsActive\":true");
            jsonResult.Should().Contain("\"NullableInt\":null");
            // Checks for standard ISO 8601 DateTime format (UTC 'Z' indicator).
            jsonResult.Should().Contain("\"Date\":\"2024-01-01T12:00:00Z\"");
        }

        [Fact]
        public void SerializePropertyValueDictionary_WithNullDictionary_ShouldReturnNull()
        {
            // Act
            var jsonResult = _auditHelperService.SerializePropertyValueDictionary(null);

            // Assert
            jsonResult.Should().BeNull(); // Expects null output for null input.
        }

        [Fact]
        public void SerializePropertyValueDictionary_WithEmptyDictionary_ShouldReturnNull()
        {
            // Arrange
            var dict = new Dictionary<string, object?>();

            // Act
            var jsonResult = _auditHelperService.SerializePropertyValueDictionary(dict);

            // Assert
            // Expects null output when the input dictionary is empty.
            jsonResult.Should().BeNull();
        }

        [Fact]
        public void SerializePropertyValueDictionary_WithError_ShouldHandleGracefully()
        {
            // Arrange
            // Creates a dictionary with a value that might cause serialization issues
            // depending on System.Text.Json configuration (plain object without specific converter).
            var dict = new Dictionary<string, object?> { { "Problem", new object() } };

            // Act
            // Note: Forcing a serialization error reliably in a unit test without complex objects
            // or modifying the service's internal JsonSerializerOptions is difficult.
            // This test primarily ensures the method doesn't throw an unhandled exception.
            // The service is expected to log the error internally if serialization fails.
            Func<string?> act = () => _auditHelperService.SerializePropertyValueDictionary(dict);

            // Assert
            // We assert that the call does not throw an exception, implying graceful handling.
            // The actual return value might be null or an error placeholder depending on implementation,
            // but the key is preventing an unhandled exception.
            act.Should().NotThrow();
        }
    }
}