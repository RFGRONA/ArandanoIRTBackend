using ArandanoIRT_Backend.Domain.IRepositories;
using ArandanoIRT_Backend.Infrastructure.Data;
using ArandanoIRT_Backend.Infrastructure.Repositories;
using ArandanoIRT_Backend.Tests.Integration.Common;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Microsoft.Extensions.Logging.Abstractions;
using ArandanoIRT_Backend.Application.Interfaces.Utilities;
using Moq;

namespace ArandanoIRT_Backend.Tests.Integration.Repositories
{
    /// <summary>
    /// Contains integration tests for the <see cref="CropRepository"/>
    /// using an SQLite in-memory database.
    /// </summary>
    public class CropRepositoryTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ApplicationDbContext _context;
        private readonly Mock<IDateTimeProvider> _mockDateTimeProvider;
        private readonly ILogger<CropRepository> _mockLogger;
        private readonly CropRepository _cropRepository;
        // An Admin Person must exist for potential FK constraints.
        private const int DEFAULT_TEST_PERSON_ID = 1;
        // Represents a fixed point in time for predictable test outcomes.
        private readonly DateTime _mockUtcNow = new DateTime(2024, 5, 1, 12, 0, 0, DateTimeKind.Utc); // Fixed time

        /// <summary>
        /// Initializes a new instance of the <see cref="CropRepositoryTests"/> class,
        /// setting up the in-memory database, mocks, and repository under test.
        /// </summary>
        public CropRepositoryTests()
        {
            (_context, _connection) = DbContextTestHelper.CreateInMemoryDbContext();
            _mockLogger = NullLogger<CropRepository>.Instance;
            _mockDateTimeProvider = new Mock<IDateTimeProvider>();
            // Sets up the mock time provider.
            _mockDateTimeProvider.Setup(dt => dt.GetUtcNow()).Returns(_mockUtcNow);

            _cropRepository = new CropRepository(_context, _mockDateTimeProvider.Object, _mockLogger);

            // Seeds the necessary Person record for FK constraints if AdminUserId is used.
            // Runs synchronously for constructor setup.
            SeedDefaultPersonAsync().Wait();
        }

        /// <summary>
        /// Disposes the database context and connection used by the test instance.
        /// </summary>
        public void Dispose()
        {
            _context?.Dispose();
            _connection?.Dispose();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Seeds a default Person record (potential admin) into the database if it does not already exist.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task SeedDefaultPersonAsync()
        {
            if (!await _context.Person.AnyAsync(p => p.Idperson == DEFAULT_TEST_PERSON_ID))
            {
                // Sets a specific ID for the default person.
                _context.Person.Add(new Infrastructure.Data.Person
                {
                    Idperson = DEFAULT_TEST_PERSON_ID,
                    Firstname = "Crop",
                    Lastname = "Admin",
                    Email = "crop.admin@example.com",
                    Password = "hashed", // Placeholder password
                    Createdat = _mockUtcNow.AddDays(-1),
                    // The CropId for this admin user might be null or related elsewhere.
                });
                await _context.SaveChangesAsync();
                _context.ChangeTracker.Clear();
            }
        }

        /// <summary>
        /// Creates a test <see cref="Domain.Entities.CropEntity"/> instance with default or specified values.
        /// </summary>
        /// <param name="id">The ID for the crop entity.</param>
        /// <param name="name">The name of the crop.</param>
        /// <param name="address">The address of the crop.</param>
        /// <param name="city">The city where the crop is located.</param>
        /// <param name="adminId">The ID of the admin user associated with the crop.</param>
        /// <returns>A new domain CropEntity instance.</returns>
        private static Domain.Entities.CropEntity CreateTestDomainCrop(
            int id = 0, string name = "Test Domain Crop", string address = "123 Domain St",
            string city = "Domain City", int? adminId = DEFAULT_TEST_PERSON_ID)
        {
            return new Domain.Entities.CropEntity(
             idCrop: id,
             nameCrop: name,
             addressCrop: address,
             cityName: city,
             createdAt: DateTime.UtcNow.AddMinutes(-5), // Ensure CreatedAt is set
             adminUserId: adminId
            );

        }

        /// <summary>
        /// Creates a test <see cref="Infrastructure.Data.Crop"/> EF Core entity instance,
        /// typically used for seeding the database.
        /// </summary>
        /// <param name="name">The name of the crop.</param>
        /// <param name="address">The address of the crop.</param>
        /// <param name="city">The city where the crop is located.</param>
        /// <param name="adminId">The ID of the admin user associated with the crop.</param>
        /// <returns>A new EF Core Crop instance.</returns>
        private Infrastructure.Data.Crop CreateTestEfCrop(
            string name = "Test EF Crop", string address = "456 EF Ave",
            string city = "EF City", int? adminId = DEFAULT_TEST_PERSON_ID)
        {
            return new Infrastructure.Data.Crop
            {
                Namecrop = name,
                Addresscrop = address,
                Cityname = city,
                Createdat = _mockUtcNow.AddMinutes(-10), 
                Adminuserid = adminId
            };
        }

        // --- Test Cases ---

        [Fact]
        public async Task Create_ShouldAddCropToDatabase()
        {
            // Arrange
            var cropName = "New Test Crop";
            var domainCrop = CreateTestDomainCrop(name: cropName);
            _mockDateTimeProvider.Setup(dt => dt.GetUtcNow()).Returns(DateTime.UtcNow); // Ensure time is set

            // Act
            var result = await _cropRepository.Create(domainCrop);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue("Repository Create should succeed");
            result.Value.Should().NotBeNull();
            result.Value.IdCrop.Should().BeGreaterThan(0);
            result.Value.NameCrop.Should().Be(cropName);

            // Verify persistence

            var verifyOptions = DbContextTestHelper.CreateOptionsForExistingConnection(_connection);
            using (var verifyContext = new ApplicationDbContext(verifyOptions))
            {
                var efCrop = await verifyContext.Crop.FindAsync(result.Value.IdCrop);
                efCrop.Should().NotBeNull();
                efCrop!.Namecrop.Should().Be(cropName);
                efCrop.Adminuserid.Should().Be(DEFAULT_TEST_PERSON_ID);
                efCrop.Cityname.Should().Be(domainCrop.CityName);
            }
        }

        [Fact]
        public async Task GetById_WhenCropExists_ShouldReturnCrop()
        {
            // Arrange
            var cropName = "GetById Crop";
            var efCrop = CreateTestEfCrop(name: cropName);
            _context.Crop.Add(efCrop);
            await _context.SaveChangesAsync();
            var cropId = efCrop.Idcrop;

            // Act
            var result = await _cropRepository.GetById(cropId);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNull();
            result.Value!.IdCrop.Should().Be(cropId);
            result.Value.NameCrop.Should().Be(cropName);
        }

        [Fact]
        public async Task GetById_WhenCropDoesNotExist_ShouldReturnFailure()
        {
            // Arrange
            var nonExistentId = 999;

            // Act
            var result = await _cropRepository.GetById(nonExistentId);

            // Assert
            result.Should().NotBeNull();
            result.IsFailure.Should().BeTrue();
            result.ErrorMessage.Should().Contain("not found");
        }

        [Fact]
        public async Task GetByNameAsync_WhenCropExists_ShouldReturnCrop()
        {
            // Arrange
            var cropName = "GetByName Crop";
            var efCrop = CreateTestEfCrop(name: cropName);
            _context.Crop.Add(efCrop);
            await _context.SaveChangesAsync();
            var cropId = efCrop.Idcrop;

            // Act
            var result = await _cropRepository.GetByNameAsync(cropName);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNull();
            result.Value!.IdCrop.Should().Be(cropId);
            result.Value.NameCrop.Should().Be(cropName);
        }

        [Fact]
        public async Task GetByNameAsync_WhenCropDoesNotExist_ShouldReturnFailure()
        {
            // Arrange
            var nonExistentName = "NonExistentCropName";

            // Act
            var result = await _cropRepository.GetByNameAsync(nonExistentName);

            // Assert
            result.Should().NotBeNull();
            result.IsFailure.Should().BeTrue();
            result.ErrorMessage.Should().Contain("not found");
        }

        [Fact]
        public async Task Update_ShouldModifyCropInDatabase()
        {
            // Arrange
            var initialName = "Initial Crop Name";
            var updatedName = "Updated Crop Name";
            var updatedCity = "Updated City";
            var efCrop = CreateTestEfCrop(name: initialName);
            _context.Crop.Add(efCrop);
            await _context.SaveChangesAsync();
            var cropId = efCrop.Idcrop;
            // Detaches the entity to simulate a realistic update scenario.
            _context.ChangeTracker.Clear();

            // Fetches the existing domain entity to simulate a modification flow.
            var getResult = await _cropRepository.GetById(cropId);
            getResult.IsSuccess.Should().BeTrue();

            // Recreates the domain entity with updated values.
            var domainCropToUpdate = CreateTestDomainCrop(
                id: getResult.Value!.IdCrop,
                // Applies the updated name.
                name: updatedName,
                address: getResult.Value.AddressCrop, // Keeps original address
                                                      // Applies the updated city.
                city: updatedCity,
                adminId: getResult.Value.AdminUserId // Keeps original admin user
            );
            // Sets the mock time for the update operation.
            _mockDateTimeProvider.Setup(dt => dt.GetUtcNow()).Returns(_mockUtcNow);

            // Act
            // The repository's Update method is expected to call SaveChanges internally.
            var updateResult = await _cropRepository.Update(domainCropToUpdate);

            // Assert
            updateResult.IsSuccess.Should().BeTrue();
            // Checks if the update operation reported affecting rows.
            updateResult.Value.Should().BeTrue();

            // Verify persistence using a separate context instance.
            var verifyOptions = DbContextTestHelper.CreateOptionsForExistingConnection(_connection);
            using (var verifyContext = new ApplicationDbContext(verifyOptions))
            {
                var updatedEfCrop = await verifyContext.Crop.FindAsync(cropId);
                updatedEfCrop.Should().NotBeNull();
                updatedEfCrop!.Namecrop.Should().Be(updatedName);
                updatedEfCrop.Cityname.Should().Be(updatedCity);
                updatedEfCrop.Updatedat.Should().NotBeNull();
                // Verifies the update timestamp matches the mock time.
                updatedEfCrop.Updatedat.Should().BeCloseTo(_mockUtcNow, TimeSpan.FromSeconds(1));
            }
        }

        [Fact]
        public async Task Delete_ShouldRemoveCropFromDatabase()
        {
            // Arrange
            var efCrop = CreateTestEfCrop(name: "Crop To Delete");
            _context.Crop.Add(efCrop);
            await _context.SaveChangesAsync();
            var cropId = efCrop.Idcrop;

            // Act
            // The repository's Delete method is expected to call SaveChanges internally.
            var deleteResult = await _cropRepository.Delete(cropId);

            // Assert
            deleteResult.IsSuccess.Should().BeTrue();
            // Checks if the delete operation reported affecting rows.
            deleteResult.Value.Should().BeTrue();

            // Verify removal using a separate context instance.
            var verifyOptions = DbContextTestHelper.CreateOptionsForExistingConnection(_connection);
            using (var verifyContext = new ApplicationDbContext(verifyOptions))
            {
                var deletedEfCrop = await verifyContext.Crop.FindAsync(cropId);
                deletedEfCrop.Should().BeNull();
            }
        }

        [Fact]
        public async Task GetAll_ShouldReturnAllCrops()
        {
            // Arrange
            var crop1 = CreateTestEfCrop(name: "GetAll Crop 1");
            // Includes a crop with a null admin for comprehensive testing.
            var crop2 = CreateTestEfCrop(name: "GetAll Crop 2", adminId: null);
            _context.Crop.AddRange(crop1, crop2);
            await _context.SaveChangesAsync();

            // Act
            var result = await _cropRepository.GetAll();

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNull();
            result.Value.Should().HaveCount(2);
            result.Value.Should().Contain(c => c.NameCrop == "GetAll Crop 1");
            result.Value.Should().Contain(c => c.NameCrop == "GetAll Crop 2" && c.AdminUserId == null);
        }
    }
}