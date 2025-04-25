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
    /// Contains integration tests for the <see cref="RefreshTokenRepository"/>
    /// using an SQLite in-memory database.
    /// </summary>
    public class RefreshTokenRepositoryTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<RefreshTokenRepository> _mockLogger;
        private readonly Mock<IDateTimeProvider> _mockDateTimeProvider;
        private readonly RefreshTokenRepository _refreshTokenRepository;
        // A Person entity must exist for foreign key constraints.
        private const int DEFAULT_TEST_PERSON_ID = 1;
        // Defines a fixed date for the mock time provider.
        private readonly DateTime _mockUtcNow = new DateTime(2024, 5, 1, 13, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// Initializes a new instance of the <see cref="RefreshTokenRepositoryTests"/> class,
        /// setting up the in-memory database, mocks, and repository under test.
        /// </summary>
        public RefreshTokenRepositoryTests()
        {
            (_context, _connection) = DbContextTestHelper.CreateInMemoryDbContext();
            _mockLogger = NullLogger<RefreshTokenRepository>.Instance;
            _mockDateTimeProvider = new Mock<IDateTimeProvider>();
            // Configures the mock time provider to return the fixed date.
            _mockDateTimeProvider.Setup(dt => dt.GetUtcNow()).Returns(_mockUtcNow);
            _refreshTokenRepository = new RefreshTokenRepository(_context, _mockDateTimeProvider.Object, _mockLogger);

            // Seeds the necessary Person record for FK constraints.
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
        /// Seeds a default Person record into the database if it does not already exist.
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
                    Firstname = "Token",
                    Lastname = "Owner",
                    Email = "token.owner@example.com",
                    Password = "hashed", // Placeholder password
                    Createdat = _mockUtcNow.AddMinutes(-10) // Seed slightly before tokens
                });
                await _context.SaveChangesAsync();
                _context.ChangeTracker.Clear();
            }
        }

        /// <summary>
        /// Creates a test <see cref="Domain.Entities.RefreshTokenEntity"/> instance.
        /// </summary>
        /// <param name="id">The ID for the entity.</param>
        /// <param name="tokenValue">The refresh token string.</param>
        /// <param name="personId">The associated Person ID.</param>
        /// <param name="sessionId">The session identifier.</param>
        /// <param name="revokedAt">The timestamp when the token was revoked, if applicable.</param>
        /// <param name="replacedBy">The token that replaced this one, if applicable.</param>
        /// <returns>A new domain RefreshTokenEntity instance.</returns>
        private Domain.Entities.RefreshTokenEntity CreateTestDomainToken(
            int id = 0, string tokenValue = "test-token", int? personId = DEFAULT_TEST_PERSON_ID,
            long sessionId = 0, DateTime? revokedAt = null, string? replacedBy = null)
        {
            // Generates a session ID based on current time if not provided.
            sessionId = (sessionId == 0) ? _mockUtcNow.Ticks : sessionId;
            var createdAt = _mockUtcNow.AddMinutes(-5); // Consistent creation time relative to mock 'now'
            var expiresAt = _mockUtcNow.AddDays(7);    // Consistent expiry time relative to mock 'now'

            return new Domain.Entities.RefreshTokenEntity(
                idRefreshToken: id,
                session: sessionId,
                token: tokenValue,
                deviceInfo: "Test Device",
                ipAddress: "127.0.0.1",
                userAgent: "Test Agent",
                createdAt: createdAt,
                expiresAt: expiresAt,
                revokedAt: revokedAt,
                revokedByIp: revokedAt.HasValue ? "127.0.0.1" : null,
                replacedByToken: replacedBy,
                personId: personId
            );
        }

        /// <summary>
        /// Creates a test <see cref="Infrastructure.Data.Refreshtoken"/> EF Core entity instance,
        /// typically used for seeding the database.
        /// </summary>
        /// <param name="tokenValue">The refresh token string.</param>
        /// <param name="personId">The associated Person ID.</param>
        /// <param name="sessionId">The session identifier.</param>
        /// <param name="revokedAt">The timestamp when the token was revoked, if applicable.</param>
        /// <param name="replacedBy">The token that replaced this one, if applicable.</param>
        /// <param name="expiresAt">The expiration timestamp.</param>
        /// <returns>A new EF Core Refreshtoken instance.</returns>
        private Infrastructure.Data.Refreshtoken CreateTestEfToken(
             string tokenValue = "ef-test-token", int? personId = DEFAULT_TEST_PERSON_ID,
             long sessionId = 0, DateTime? revokedAt = null, string? replacedBy = null, DateTime? expiresAt = null)
        {
            // Generates a session ID based on current time if not provided.
            sessionId = (sessionId == 0) ? _mockUtcNow.Ticks : sessionId;
            var createdAt = _mockUtcNow.AddMinutes(-10); // Seed slightly before mock 'now'

            return new Infrastructure.Data.Refreshtoken
            {
                Session = sessionId,
                // Notes the difference in property names between domain and EF entities.
                Refreshtoken1 = tokenValue,
                Deviceinfo = "EF Test Device",
                Ipaddress = "192.168.1.1",
                Useragent = "EF Test Agent",
                Createdat = createdAt,
                Expiresat = expiresAt ?? _mockUtcNow.AddDays(7),
                Revokedat = revokedAt,
                Revokedbyip = revokedAt.HasValue ? "192.168.1.1" : null,
                Replacedbytoken = replacedBy,
                Personid = personId
            };
        }

        // --- Test Cases ---

        [Fact]
        public async Task Create_ShouldAddRefreshTokenToDatabase()
        {
            // Arrange
            var tokenValue = "create-refresh-token";
            var domainToken = CreateTestDomainToken(tokenValue: tokenValue);

            // Act
            // The repository's Create method signature is async Task<Result<RefreshTokenEntity>>.
            var result = await _refreshTokenRepository.Create(domainToken);
            // Assumes, based on other repositories, that Create does not call SaveChangesAsync.
            // Saves changes potentially made by the Create method (e.g., marking entity as Added).
            await _context.SaveChangesAsync();

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue("The repository Create operation should succeed.");
            result.Value.Should().NotBeNull();
            // Verifies an ID is assigned by the database.
            result.Value.IdRefreshToken.Should().BeGreaterThan(0);
            result.Value.Token.Should().Be(tokenValue);

            // Verify persistence using a separate context instance.
            var verifyOptions = DbContextTestHelper.CreateOptionsForExistingConnection(_connection);
            using (var verifyContext = new ApplicationDbContext(verifyOptions))
            {
                // Uses the corresponding EF entity and property names for verification.
                var efToken = await verifyContext.Refreshtoken.FindAsync(result.Value.IdRefreshToken);
                efToken.Should().NotBeNull();
                // Checks the correct property on the EF entity.
                efToken!.Refreshtoken1.Should().Be(tokenValue);
                efToken.Personid.Should().Be(DEFAULT_TEST_PERSON_ID);
                efToken.Session.Should().Be(domainToken.Session);
                // Verifies the creation timestamp matches the domain object's time.
                efToken.Createdat.Should().BeCloseTo(domainToken.CreatedAt, TimeSpan.FromSeconds(1));
            }
        }

        [Fact]
        public async Task GetByTokenAsync_WhenTokenExistsAndIsValid_ShouldReturnToken()
        {
            // Arrange
            var tokenValue = "get-by-token-valid";
            var session = _mockUtcNow.Ticks; // Use mock time for session ID consistency
            var efToken = CreateTestEfToken(tokenValue: tokenValue, sessionId: session);
            _context.Refreshtoken.Add(efToken);
            await _context.SaveChangesAsync();

            // Act
            var result = await _refreshTokenRepository.GetByTokenAsync(tokenValue);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNull();
            result.Value!.Token.Should().Be(tokenValue); // Checks domain entity property
            result.Value.Session.Should().Be(session);
            result.Value.IdRefreshToken.Should().Be(efToken.Idrefreshtoken);
            result.Value.RevokedAt.Should().BeNull(); // Expects token not to be revoked
            result.Value.ExpiresAt.Should().Be(efToken.Expiresat); // Checks expiry date
            // Verifies token is not expired relative to mock time.
            result.Value.ExpiresAt.Should().BeAfter(_mockUtcNow);
        }

        [Fact]
        public async Task GetByTokenAsync_WhenTokenDoesNotExist_ShouldReturnFailure()
        {
            // Arrange
            var nonExistentToken = "does-not-exist";

            // Act
            var result = await _refreshTokenRepository.GetByTokenAsync(nonExistentToken);

            // Assert
            result.Should().NotBeNull();
            result.IsFailure.Should().BeTrue();
            // Checks for the expected error message content.
            result.ErrorMessage.Should().Contain("not found");
        }

        [Fact]
        public async Task Update_ShouldModifyTokenInDatabase()
        {
            // Arrange
            var tokenValue = "update-token";
            var efToken = CreateTestEfToken(tokenValue: tokenValue);
            _context.Refreshtoken.Add(efToken);
            await _context.SaveChangesAsync();
            var tokenId = efToken.Idrefreshtoken;

            // Fetches the domain entity to simulate a typical modification flow.
            var getResult = await _refreshTokenRepository.GetByTokenAsync(tokenValue);
            getResult.IsSuccess.Should().BeTrue();
            var domainTokenToUpdate = getResult.Value!;

            // Modifies the fetched domain entity.
            var revokeTime = _mockUtcNow; // Use mock time for revoke action
            var revokeIp = "1.2.3.4";
            var replacementToken = "new-replacement-token";
            domainTokenToUpdate.Revoke(revokeTime, revokeIp);
            domainTokenToUpdate.Replace(replacementToken);

            // Act
            // Assumes Update accepts a domain entity and handles mapping/saving.
            // The exact behavior depends on the RefreshTokenRepository.Update implementation.
            // Assumes the Update method returns an async Task<Result>.
            var updateResult = await _refreshTokenRepository.Update(domainTokenToUpdate);

            // Assert
            updateResult.IsSuccess.Should().BeTrue(); // Assumes Update returns success

            // Verify persistence using the same context after clearing the tracker.
            _context.ChangeTracker.Clear(); // Detach entities to ensure fresh load
            var updatedEfToken = await _context.Refreshtoken.FindAsync(tokenId);
            updatedEfToken.Should().NotBeNull();
            // Verifies the revoked timestamp matches the mock time closely.
            updatedEfToken!.Revokedat.Should().BeCloseTo(revokeTime, TimeSpan.FromSeconds(1));
            updatedEfToken.Revokedbyip.Should().Be(revokeIp);
            updatedEfToken.Replacedbytoken.Should().Be(replacementToken);
        }
    }
}