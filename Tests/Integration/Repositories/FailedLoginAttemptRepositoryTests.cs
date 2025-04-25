using ArandanoIRT_Backend.Domain.Entities;
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
    /// Contains integration tests for the <see cref="FailedLoginAttemptRepository"/>
    /// using an SQLite in-memory database.
    /// </summary>
    public class FailedLoginAttemptRepositoryTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ApplicationDbContext _context;
        private readonly Mock<IDateTimeProvider> _mockDateTimeProvider;
        private readonly ILogger<FailedLoginAttemptRepository> _mockLogger;
        private readonly FailedLoginAttemptRepository _attemptRepository;
        private const int DEFAULT_TEST_PERSON_ID = 1;
        // Defines a fixed date for the mock time provider to ensure deterministic tests.
        private readonly DateTime _mockUtcNow = new DateTime(2024, 4, 23, 10, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// Initializes a new instance of the <see cref="FailedLoginAttemptRepositoryTests"/> class,
        /// setting up the in-memory database, mocks, and repository under test.
        /// </summary>
        public FailedLoginAttemptRepositoryTests()
        {
            (_context, _connection) = DbContextTestHelper.CreateInMemoryDbContext();
            _mockLogger = NullLogger<FailedLoginAttemptRepository>.Instance;
            _mockDateTimeProvider = new Mock<IDateTimeProvider>();
            // Configures the mock time provider to return the fixed date.
            _mockDateTimeProvider.Setup(dt => dt.GetUtcNow()).Returns(_mockUtcNow);

            _attemptRepository = new FailedLoginAttemptRepository(_context, _mockDateTimeProvider.Object, _mockLogger);

            // Seeds the default person required for tests.
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
        /// Seeds a default Person record into the database if it does not already exist.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task SeedDefaultPersonAsync()
        {
            if (!await _context.Person.AnyAsync(p => p.Idperson == DEFAULT_TEST_PERSON_ID))
            {
                _context.Person.Add(new Infrastructure.Data.Person
                {
                    Idperson = DEFAULT_TEST_PERSON_ID,
                    Firstname = "Login",
                    Lastname = "Attempter",
                    Email = "login.attempter@example.com",
                    Password = "hashed", // Placeholder password
                    // Uses UtcNow for the seed creation time, distinct from the mock time.
                    Createdat = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
                _context.ChangeTracker.Clear();
            }
        }

        /// <summary>
        /// Creates a test <see cref="Domain.Entities.FailedLoginAttemptEntity"/> instance
        /// without explicitly setting the AttemptDate.
        /// </summary>
        /// <param name="id">The ID for the entity.</param>
        /// <param name="personId">The associated Person ID.</param>
        /// <param name="ip">The IP address for the attempt.</param>
        /// <returns>A new domain FailedLoginAttemptEntity instance.</returns>
        /// <remarks>
        /// The AttemptDate is expected to be set by the repository using the IDateTimeProvider.
        /// </remarks>
        private static Domain.Entities.FailedLoginAttemptEntity CreateTestDomainAttempt(
            int id = 0, int personId = DEFAULT_TEST_PERSON_ID, string ip = "127.0.0.1")
        {
            // Allows the repository to set the AttemptDate using the mock time provider.
            return new Domain.Entities.FailedLoginAttemptEntity(
                idFailedLoginAttempt: id,
                attemptDate: default, // Lets the repository handle this via mock provider.
                ipAddress: ip,
                deviceInfo: "Test Device Info",
                userAgent: "Test User Agent",
                personId: personId
            );
        }

        /// <summary>
        /// Creates a test <see cref="Infrastructure.Data.Failedloginattempt"/> EF Core entity instance,
        /// typically used for seeding the database.
        /// </summary>
        /// <param name="personId">The associated Person ID.</param>
        /// <param name="ip">The IP address for the attempt.</param>
        /// <param name="attemptDate">The timestamp of the attempt. Uses UtcNow if null.</param>
        /// <returns>A new EF Core Failedloginattempt instance.</returns>
        private static Infrastructure.Data.Failedloginattempt CreateTestEfAttempt(
             int personId = DEFAULT_TEST_PERSON_ID, string ip = "192.168.0.1", DateTime? attemptDate = null)
        {
            return new Infrastructure.Data.Failedloginattempt
            {
                // Seeding operations can use UtcNow directly if a specific time is not needed.
                Attemptdate = attemptDate ?? DateTime.UtcNow,
                Ipaddress = ip,
                Deviceinfo = "EF Device Info",
                Useragent = "EF User Agent",
                Personid = personId
            };
        }

        // --- Test Cases ---

        [Fact]
        public async Task Create_ShouldAddAttemptToDatabase()
        {
            // Arrange
            var ipAddress = "1.1.1.1";
            // Creates attempt using the helper (which does not set the date).
            var domainAttempt = CreateTestDomainAttempt(ip: ipAddress);

            // The mockDateTimeProvider is already set up in the constructor.

            // Act
            var result = await _attemptRepository.Create(domainAttempt);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNull();
            result.Value!.IdFailedLoginAttempt.Should().BeGreaterThan(0);
            result.Value.IpAddress.Should().Be(ipAddress);
            result.Value.PersonId.Should().Be(DEFAULT_TEST_PERSON_ID);
            // Asserts against the mocked date provided during setup.
            result.Value.AttemptDate.Should().Be(_mockUtcNow);

            // Verify persistence using a separate context instance.
            var verifyOptions = DbContextTestHelper.CreateOptionsForExistingConnection(_connection);
            using (var verifyContext = new ApplicationDbContext(verifyOptions))
            {
                var efAttempt = await verifyContext.Failedloginattempt.FindAsync(result.Value.IdFailedLoginAttempt);
                efAttempt.Should().NotBeNull();
                efAttempt!.Ipaddress.Should().Be(ipAddress);
                efAttempt.Personid.Should().Be(DEFAULT_TEST_PERSON_ID);
                // Asserts against the mocked date for the persisted entity.
                efAttempt.Attemptdate.Should().Be(_mockUtcNow);
            }
        }

        // Seeds a second person for testing scenarios involving multiple users.
        [Fact]
        public async Task GetRecentAttemptsAsync_ShouldReturnAttemptsForPersonSinceDate()
        {
            // Arrange
            var personId = DEFAULT_TEST_PERSON_ID;
            var otherPersonId = personId + 1;
            var now = DateTime.UtcNow; // Use current time for relative seeding
            var cutoffDate = now.AddMinutes(-30);

            // Seeds the other person needed for the FK constraint.
            if (!await _context.Person.AnyAsync(p => p.Idperson == otherPersonId))
            {
                _context.Person.Add(new Infrastructure.Data.Person
                {
                    Idperson = otherPersonId,
                    Firstname = "Other",
                    Lastname = "User",
                    Email = "other@example.com",
                    Password = "p",
                    Createdat = now
                });
                // Saves the new person first.
                await _context.SaveChangesAsync();
                _context.ChangeTracker.Clear();
            }

            // Adds the login attempts for seeding.
            // Recent attempt for target person.
            var attempt1 = CreateTestEfAttempt(personId: personId, attemptDate: now.AddMinutes(-10));
            // Old attempt for target person.
            var attempt2 = CreateTestEfAttempt(personId: personId, attemptDate: now.AddMinutes(-45));
            // Recent attempt for target person.
            var attempt3 = CreateTestEfAttempt(personId: personId, attemptDate: now.AddMinutes(-5));
            // Recent attempt for a different person.
            var attemptOtherPerson = CreateTestEfAttempt(personId: otherPersonId, attemptDate: now.AddMinutes(-15));

            _context.Failedloginattempt.AddRange(attempt1, attempt2, attempt3, attemptOtherPerson);
            // Saves the login attempts.
            await _context.SaveChangesAsync();

            // Act
            var result = await _attemptRepository.GetRecentAttemptsAsync(personId, cutoffDate);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNull();
            result.Value.Should().HaveCount(2); // Only the two recent attempts for the target person
            result.Value.Should().Contain(a => a.IdFailedLoginAttempt == attempt1.Idfailedloginattempt);
            result.Value.Should().Contain(a => a.IdFailedLoginAttempt == attempt3.Idfailedloginattempt);
            // Verifies that results are ordered by date descending.
            result.Value.Should().BeInDescendingOrder(a => a.AttemptDate);
        }

        [Fact]
        public async Task GetRecentAttemptsByIpAsync_ShouldReturnAttemptsForIpSinceDate()
        {
            // Arrange
            var targetIp = "8.8.8.8";
            var otherIp = "9.9.9.9";
            var now = DateTime.UtcNow; // Use current time for relative seeding
            var cutoffDate = now.AddHours(-1);

            // Seeds another person if needed for IP testing.
            // Uses a different ID if needed to avoid collisions.
            var otherPersonIdForIpTest = DEFAULT_TEST_PERSON_ID + 2;
            if (!await _context.Person.AnyAsync(p => p.Idperson == otherPersonIdForIpTest))
            {
                _context.Person.Add(new Infrastructure.Data.Person { Idperson = otherPersonIdForIpTest, Firstname = "IP", Lastname = "Test", Email = "ip@test.com", Password = "p", Createdat = now });
                await _context.SaveChangesAsync();
                _context.ChangeTracker.Clear();
            }

            // Recent attempt from target IP.
            var attempt1 = CreateTestEfAttempt(ip: targetIp, attemptDate: now.AddMinutes(-10), personId: DEFAULT_TEST_PERSON_ID);
            // Old attempt from target IP.
            var attempt2 = CreateTestEfAttempt(ip: targetIp, attemptDate: now.AddHours(-2), personId: DEFAULT_TEST_PERSON_ID);
            // Recent attempt from another IP.
            var attempt3 = CreateTestEfAttempt(ip: otherIp, attemptDate: now.AddMinutes(-5), personId: otherPersonIdForIpTest);
            // Recent attempt from target IP.
            var attempt4 = CreateTestEfAttempt(ip: targetIp, attemptDate: now.AddMinutes(-30), personId: DEFAULT_TEST_PERSON_ID);

            _context.Failedloginattempt.AddRange(attempt1, attempt2, attempt3, attempt4);
            await _context.SaveChangesAsync();

            // Act
            var result = await _attemptRepository.GetRecentAttemptsByIpAsync(targetIp, cutoffDate);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNull();
            result.Value.Should().HaveCount(2); // Only the two recent attempts from the target IP
            result.Value.Should().Contain(a => a.IdFailedLoginAttempt == attempt1.Idfailedloginattempt);
            result.Value.Should().Contain(a => a.IdFailedLoginAttempt == attempt4.Idfailedloginattempt);
            // Verifies that results are ordered by date descending.
            result.Value.Should().BeInDescendingOrder(a => a.AttemptDate);
        }

        [Fact]
        public async Task GetById_WhenAttemptExists_ShouldReturnAttempt()
        {
            // Arrange
            var efAttempt = CreateTestEfAttempt();
            _context.Failedloginattempt.Add(efAttempt);
            await _context.SaveChangesAsync();
            var id = efAttempt.Idfailedloginattempt;

            // Act
            var result = await _attemptRepository.GetById(id);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNull();
            result.Value!.IdFailedLoginAttempt.Should().Be(id);
            result.Value.IpAddress.Should().Be(efAttempt.Ipaddress);
        }

        [Fact]
        public async Task GetById_WhenAttemptDoesNotExist_ShouldReturnFailure()
        {
            // Arrange
            var nonExistentId = 9999;

            // Act
            var result = await _attemptRepository.GetById(nonExistentId);

            // Assert
            result.Should().NotBeNull();
            result.IsFailure.Should().BeTrue();
            result.ErrorMessage.Should().Contain("not found");
        }

        [Fact]
        public async Task GetAll_ShouldReturnAllAttempts()
        {
            // Arrange
            // Seeds another person needed for the second attempt's FK constraint.
            var otherPersonIdForAll = DEFAULT_TEST_PERSON_ID + 3;
            if (!await _context.Person.AnyAsync(p => p.Idperson == otherPersonIdForAll))
            {
                _context.Person.Add(new Infrastructure.Data.Person { Idperson = otherPersonIdForAll, Firstname = "All", Lastname = "Test", Email = "all@test.com", Password = "p", Createdat = DateTime.UtcNow });
                await _context.SaveChangesAsync();
                _context.ChangeTracker.Clear();
            }

            var attempt1 = CreateTestEfAttempt(ip: "10.0.0.1", personId: DEFAULT_TEST_PERSON_ID);
            var attempt2 = CreateTestEfAttempt(ip: "10.0.0.2", personId: otherPersonIdForAll);
            _context.Failedloginattempt.AddRange(attempt1, attempt2);
            await _context.SaveChangesAsync();

            // Act
            var result = await _attemptRepository.GetAll();

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNull();
            // Note: The exact count might be higher if previous tests left data.
            // Checking for the presence of the specific entities added in this test is more robust.
            result.Value.Should().Contain(a => a.IdFailedLoginAttempt == attempt1.Idfailedloginattempt && a.IpAddress == "10.0.0.1");
            result.Value.Should().Contain(a => a.IdFailedLoginAttempt == attempt2.Idfailedloginattempt && a.IpAddress == "10.0.0.2");
        }

        [Fact]
        public async Task Update_ShouldModifyAttemptInDatabase()
        {
            // Arrange
            var originalIp = "11.0.0.1";
            var updatedIp = "11.0.0.2";
            var efAttempt = CreateTestEfAttempt(ip: originalIp);
            _context.Failedloginattempt.Add(efAttempt);
            await _context.SaveChangesAsync();
            var id = efAttempt.Idfailedloginattempt;
            // Detach the entity before creating the update object.
            _context.ChangeTracker.Clear();

            // Create domain object for update, using the original attempt date.
            var domainAttempt = new FailedLoginAttemptEntity(
                idFailedLoginAttempt: id,
                // Uses the original date from the seeded entity.
                attemptDate: efAttempt.Attemptdate,
                // Uses the updated IP address.
                ipAddress: updatedIp,
                // Provides an example update for another field.
                deviceInfo: "Updated Device",
                userAgent: efAttempt.Useragent, // Keeps original user agent
                personId: efAttempt.Personid.HasValue ? efAttempt.Personid.Value : throw new InvalidOperationException("Personid cannot be null")
            );

            // Act
            var result = await _attemptRepository.Update(domainAttempt);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeTrue(); // Checks if rows were affected.

            // Verify persistence using a separate context instance.
            var verifyOptions = DbContextTestHelper.CreateOptionsForExistingConnection(_connection);
            using (var verifyContext = new ApplicationDbContext(verifyOptions))
            {
                var updatedEfAttempt = await verifyContext.Failedloginattempt.FindAsync(id);
                updatedEfAttempt.Should().NotBeNull();
                updatedEfAttempt!.Ipaddress.Should().Be(updatedIp);
                // Checks another updated field for verification.
                updatedEfAttempt.Deviceinfo.Should().Be("Updated Device");
            }
        }

        [Fact]
        public async Task Delete_ShouldRemoveAttemptFromDatabase()
        {
            // Arrange
            var efAttempt = CreateTestEfAttempt(ip: "12.0.0.1");
            _context.Failedloginattempt.Add(efAttempt);
            await _context.SaveChangesAsync();
            var id = efAttempt.Idfailedloginattempt;

            // Act
            var result = await _attemptRepository.Delete(id);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeTrue(); // Checks if rows were affected.

            // Verify removal using a separate context instance.
            var verifyOptions = DbContextTestHelper.CreateOptionsForExistingConnection(_connection);
            using (var verifyContext = new ApplicationDbContext(verifyOptions))
            {
                var deletedEfAttempt = await verifyContext.Failedloginattempt.FindAsync(id);
                deletedEfAttempt.Should().BeNull();
            }
        }
    }
}