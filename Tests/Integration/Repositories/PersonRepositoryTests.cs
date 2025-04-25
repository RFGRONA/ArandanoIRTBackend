using ArandanoIRT_Backend.Domain.IRepositories;
using ArandanoIRT_Backend.Infrastructure.Data;
using ArandanoIRT_Backend.Infrastructure.Repositories;
using ArandanoIRT_Backend.Tests.Integration.Common;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;
using ArandanoIRT_Backend.Application.Interfaces.Utilities;
using Moq;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArandanoIRT_Backend.Tests.Integration.Repositories
{
    /// <summary>
    /// Contains integration tests for the <see cref="PersonRepository"/>
    /// using an SQLite in-memory database.
    /// </summary>
    public class PersonRepositoryTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ApplicationDbContext _context;
        private readonly Mock<IDateTimeProvider> _mockDateTimeProvider;
        private readonly ILogger<PersonRepository> _mockLogger;
        private readonly PersonRepository _personRepository;
        private const int DEFAULT_TEST_CROP_ID = 1;
        // Defines a fixed date for the mock time provider.
        private readonly DateTime _mockUtcNow = new DateTime(2024, 5, 1, 12, 30, 0, DateTimeKind.Utc);

        /// <summary>
        /// Initializes a new instance of the <see cref="PersonRepositoryTests"/> class,
        /// setting up the in-memory database, mocks, and repository under test.
        /// </summary>
        public PersonRepositoryTests()
        {
            (_context, _connection) = DbContextTestHelper.CreateInMemoryDbContext();
            _mockDateTimeProvider = new Mock<IDateTimeProvider>();
            _mockLogger = NullLogger<PersonRepository>.Instance;
            // Configures the mock time provider to return the fixed date.
            _mockDateTimeProvider.Setup(dt => dt.GetUtcNow()).Returns(_mockUtcNow);
            _personRepository = new PersonRepository(_context, _mockDateTimeProvider.Object, _mockLogger);
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
        /// Creates a test <see cref="Domain.Entities.PersonEntity"/> instance.
        /// </summary>
        /// <param name="email">The email address for the person.</param>
        /// <param name="firstName">The first name.</param>
        /// <param name="lastName">The last name.</param>
        /// <param name="cropId">The associated Crop ID.</param>
        /// <param name="id">The ID for the entity.</param>
        /// <returns>A new domain PersonEntity instance.</returns>
        private Domain.Entities.PersonEntity CreateTestDomainPerson(string email, string firstName = "Test", string lastName = "User", int? cropId = DEFAULT_TEST_CROP_ID, int id = 0)
        {
            // Uses the mock time provider for the creation timestamp.
            return new Domain.Entities.PersonEntity(
                idPerson: id, firstName: firstName, lastName: lastName, email: email, password: "hashed_password",
                createdAt: _mockUtcNow, isAdmin: false, allNotifications: true, cropId: cropId
            );
        }

        /// <summary>
        /// Creates a test <see cref="Infrastructure.Data.Person"/> EF Core entity instance,
        /// typically used for seeding the database or direct context operations.
        /// </summary>
        /// <param name="email">The email address for the person.</param>
        /// <param name="firstName">The first name.</param>
        /// <param name="lastName">The last name.</param>
        /// <param name="cropId">The associated Crop ID.</param>
        /// <returns>A new EF Core Person instance.</returns>
        private Infrastructure.Data.Person CreateTestEfPerson(string email, string firstName = "Test", string lastName = "User", int? cropId = DEFAULT_TEST_CROP_ID)
        {
            // Seeding might use a slightly different time or the mock time.
            return new Infrastructure.Data.Person
            {
                Firstname = firstName,
                Lastname = lastName,
                Email = email,
                Password = "hashed_password", // Placeholder password
                Createdat = _mockUtcNow.AddMinutes(-2), // Example: Seed slightly before mock 'now'
                Isadmin = false,
                Allnotifications = true,
                Cropid = cropId
            };
        }

        /// <summary>
        /// Seeds a default Crop record into the database if it does not already exist.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task SeedDefaultCropAsync()
        {
            if (!await _context.Crop.AnyAsync(c => c.Idcrop == DEFAULT_TEST_CROP_ID))
            {
                // Use the constant ID for the default crop.
                _context.Crop.Add(new Infrastructure.Data.Crop
                {
                    Idcrop = DEFAULT_TEST_CROP_ID,
                    Namecrop = "Test Seed Crop",
                    Addresscrop = "123 Seed St",
                    Cityname = "Seed City",
                    // Creates the crop slightly before related Person entities might be created.
                    Createdat = _mockUtcNow.AddMinutes(-5),
                    // The AdminUserId might need seeding if it's a required foreign key.
                    // Adminuserid = ... // Example if needed
                });
                await _context.SaveChangesAsync();
                // Clears the change tracker after seeding to avoid side effects.
                _context.ChangeTracker.Clear();
            }
        }

        [Fact]
        public async Task Create_ShouldAddPersonToDatabase()
        {
            // Arrange
            await SeedDefaultCropAsync(); // Ensures the related Crop exists.
            var email = "create.test@example.com";
            var personDomainToCreate = CreateTestDomainPerson(email);

            // Act
            // The Create method calls SaveChangesAsync internally.
            var result = await _personRepository.Create(personDomainToCreate);

            // Assert
            // 1. Verify the result from the Create method.
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNull();
            // Verifies that an ID was assigned by the database.
            result.Value!.IdPerson.Should().BeGreaterThan(0);

            // 2. Verify persistence using a separate context attached to the same connection.
            var verifyOptions = DbContextTestHelper.CreateOptionsForExistingConnection(_connection);
            using (var verifyContext = new ApplicationDbContext(verifyOptions))
            {
                var retrievedPerson = await verifyContext.Person.FirstOrDefaultAsync(p => p.Email == email);
                retrievedPerson.Should().NotBeNull();
                retrievedPerson!.Firstname.Should().Be(personDomainToCreate.FirstName);
                retrievedPerson.Lastname.Should().Be(personDomainToCreate.LastName);
                retrievedPerson.Cropid.Should().Be(personDomainToCreate.CropId);
                // Compares the retrieved ID with the ID returned by the Create method.
                retrievedPerson.Idperson.Should().Be(result.Value.IdPerson);
                // Checks if the CreatedAt timestamp matches the mock time.
                retrievedPerson.Createdat.Should().BeCloseTo(_mockUtcNow, TimeSpan.FromSeconds(1));
            }
        }

        [Fact]
        public async Task GetById_WhenPersonExists_ShouldReturnPerson()
        {
            // Arrange
            // Seeds the required Crop entity first.
            await SeedDefaultCropAsync();
            var email = "getbyid.test@example.com";
            // Uses the default CropId specified in the helper.
            var personEf = CreateTestEfPerson(email);
            _context.Person.Add(personEf);
            await _context.SaveChangesAsync();
            var expectedId = personEf.Idperson;

            // Act
            var result = await _personRepository.GetById(expectedId);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNull();
            result.Value!.IdPerson.Should().Be(expectedId);
            result.Value.Email.Should().Be(email);
        }

        // Test case for GetById when person does not exist.
        [Fact]
        public async Task GetById_WhenPersonDoesNotExist_ShouldReturnFailure()
        {
            // Arrange
            var nonExistentId = 999;

            // Act
            var result = await _personRepository.GetById(nonExistentId);

            // Assert
            result.Should().NotBeNull();
            result.IsFailure.Should().BeTrue();
            result.ErrorMessage.Should().Contain("not found");
        }

        [Fact]
        public async Task GetByEmailAsync_WhenPersonExists_ShouldReturnPerson()
        {
            // Arrange
            // Seeds the required Crop entity first.
            await SeedDefaultCropAsync();
            var email = "getbyemail.test@example.com";
            // Uses the default CropId specified in the helper.
            var personEf = CreateTestEfPerson(email);
            _context.Person.Add(personEf);
            await _context.SaveChangesAsync();

            // Act
            var result = await _personRepository.GetByEmailAsync(email);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNull();
            result.Value!.Email.Should().Be(email);
            result.Value.IdPerson.Should().Be(personEf.Idperson);
        }

        // Test case for GetByEmailAsync when person does not exist.
        [Fact]
        public async Task GetByEmailAsync_WhenPersonDoesNotExist_ShouldReturnFailure()
        {
            // Arrange
            var nonExistentEmail = "not.found@example.com";

            // Act
            var result = await _personRepository.GetByEmailAsync(nonExistentEmail);

            // Assert
            result.Should().NotBeNull();
            result.IsFailure.Should().BeTrue();
            result.ErrorMessage.Should().Contain("not found");
        }

        [Fact]
        public async Task Update_EFEntityDirectly_ShouldModifyPersonInDatabase()
        {
            // Arrange
            await SeedDefaultCropAsync(); // Ensure crop exists
            var email = "update.test@example.com";
            var originalLastName = "Original";
            var updatedLastName = "Updated";
            var personEf = CreateTestEfPerson(email, lastName: originalLastName);
            _context.Person.Add(personEf);
            await _context.SaveChangesAsync();
            var personId = personEf.Idperson;
            // Ensures an ID was assigned during SaveChanges.
            personId.Should().BeGreaterThan(0);

            // Act
            // Uses the same context instance (_context) to find and modify the entity.
            var personToUpdate = await _context.Person.FindAsync(personId);
            personToUpdate.Should().NotBeNull("Person must exist in the context before update");
            personToUpdate!.Lastname = updatedLastName;
            // Sets the update timestamp using the mock provider.
            personToUpdate.Updatedat = _mockDateTimeProvider.Object.GetUtcNow();
            var changes = await _context.SaveChangesAsync();

            // Assert
            changes.Should().BeGreaterThan(0); // Verifies that SaveChanges affected at least one row.

            // Verification using the same connection via a new context instance.
            // Uses the existing connection.
            var verifyOptions = DbContextTestHelper.CreateOptionsForExistingConnection(_connection);
            // Creates a new context with that connection.
            using (var verifyContext = new ApplicationDbContext(verifyOptions))
            {
                var updatedPerson = await verifyContext.Person.FindAsync(personId);
                // This assertion verifies the entity still exists after update.
                updatedPerson.Should().NotBeNull("Person should still exist after update");
                updatedPerson!.Lastname.Should().Be(updatedLastName);
                // Verifies the update timestamp matches the mock time closely.
                updatedPerson!.Updatedat.Should().BeCloseTo(personToUpdate.Updatedat.Value, TimeSpan.FromSeconds(1));
            }
        }
    }
}