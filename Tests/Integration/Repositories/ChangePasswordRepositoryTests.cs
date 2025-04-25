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
    /// Contains integration tests for the <see cref="ChangePasswordRepository"/>
    /// using an SQLite in-memory database.
    /// </summary>
    public class ChangePasswordRepositoryTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ApplicationDbContext _context;
        private readonly Mock<IDateTimeProvider> _mockDateTimeProvider;
        private readonly ILogger<ChangePasswordRepository> _mockLogger;
        private readonly ChangePasswordRepository _tokenRepository;
        // A Person record needs to exist for the foreign key constraint.
        private const int DEFAULT_TEST_PERSON_ID = 1;
        // Represents a fixed point in time for predictable test outcomes.
        private readonly DateTime _mockUtcNow = new DateTime(2024, 5, 1, 12, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// Initializes a new instance of the <see cref="ChangePasswordRepositoryTests"/> class,
        /// setting up the in-memory database, mocks, and repository under test.
        /// </summary>
        public ChangePasswordRepositoryTests()
        {
            (_context, _connection) = DbContextTestHelper.CreateInMemoryDbContext();
            _mockLogger = NullLogger<ChangePasswordRepository>.Instance;
            _mockDateTimeProvider = new Mock<IDateTimeProvider>();
            // Configures the mock time provider to return a fixed UTC time.
            _mockDateTimeProvider.Setup(dt => dt.GetUtcNow()).Returns(_mockUtcNow);

            _tokenRepository = new ChangePasswordRepository(_context, _mockDateTimeProvider.Object, _mockLogger);

            // Seeds the database with a default person required by tests.
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
        /// Ensures that a default Person record exists in the database for foreign key constraints.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task SeedDefaultPersonAsync()
        {
            if (!await _context.Person.AnyAsync(p => p.Idperson == DEFAULT_TEST_PERSON_ID))
            {
                _context.Person.Add(new Infrastructure.Data.Person
                {
                    Idperson = DEFAULT_TEST_PERSON_ID,
                    Firstname = "Password",
                    Lastname = "Resetter",
                    Email = "pwd.reset@example.com",
                    Password = "hashed", // Placeholder for password
                    Createdat = _mockUtcNow.AddDays(-1)
                });
                await _context.SaveChangesAsync();
                // Detaches the seeded entity to avoid tracking issues in subsequent operations.
                _context.ChangeTracker.Clear();
            }
        }

        /// <summary>
        /// Creates a test <see cref="Domain.Entities.ChangePasswordEntity"/> instance.
        /// </summary>
        /// <param name="id">The ID for the entity.</param>
        /// <param name="personId">The associated Person ID.</param>
        /// <param name="tokenValue">The password reset token string.</param>
        /// <param name="expiresIn">The duration until the token expires.</param>
        /// <param name="createdAt">The creation timestamp.</param>
        /// <returns>A new domain ChangePasswordEntity instance.</returns>
        private Domain.Entities.ChangePasswordEntity CreateTestDomainToken(
            int id = 0, int? personId = DEFAULT_TEST_PERSON_ID, string tokenValue = "reset-token",
            TimeSpan? expiresIn = null, DateTime? createdAt = null)
        {
            var creation = createdAt ?? _mockUtcNow;
            // Sets a default expiry of 30 minutes if not specified.
            var expiry = creation.Add(expiresIn ?? TimeSpan.FromMinutes(30));

            return new Domain.Entities.ChangePasswordEntity(
                idChangePassword: id,
                passwordResetToken: tokenValue,
                resetTokenExpiresAt: expiry,
                tokenCreatedAt: creation,
                personId: personId
            );
        }

        /// <summary>
        /// Creates a test <see cref="Infrastructure.Data.Changepassword"/> EF Core entity instance,
        /// typically used for seeding the database.
        /// </summary>
        /// <param name="personId">The associated Person ID.</param>
        /// <param name="tokenValue">The password reset token string.</param>
        /// <param name="expiresIn">The duration until the token expires.</param>
        /// <param name="createdAt">The creation timestamp.</param>
        /// <returns>A new EF Core Changepassword instance.</returns>
        private Infrastructure.Data.Changepassword CreateTestEfToken(
             int? personId = DEFAULT_TEST_PERSON_ID, string tokenValue = "ef-reset-token",
             TimeSpan? expiresIn = null, DateTime? createdAt = null)
        {
            // Ensures the token is created slightly in the past relative to the mock 'now' time.
            var creation = createdAt ?? _mockUtcNow.AddMinutes(-5);
            var expiry = creation.Add(expiresIn ?? TimeSpan.FromMinutes(30));

            return new Infrastructure.Data.Changepassword
            {
                Passwordresettoken = tokenValue,
                Resettokenexpiresat = expiry,
                Tokencreatedat = creation,
                Personid = personId
            };
        }

        // --- Test Cases ---

        [Fact]
        public async Task Create_ShouldAddTokenAndDeleteExistingForPerson()
        {
            // Arrange
            var personId = DEFAULT_TEST_PERSON_ID;
            // Seeds an existing token for the target person to test replacement logic.
            var existingEfToken = CreateTestEfToken(personId: personId, tokenValue: "old-token");
            _context.Changepassword.Add(existingEfToken);
            await _context.SaveChangesAsync();
            var oldTokenId = existingEfToken.Idchangepassword;
            // Detaches the seeded entity.
            _context.ChangeTracker.Clear();

            var newTokenValue = "new-reset-token";
            var domainTokenToCreate = CreateTestDomainToken(personId: personId, tokenValue: newTokenValue);

            // Act
            // The Create method internally handles deletion of old tokens and saving changes.
            var result = await _tokenRepository.Create(domainTokenToCreate);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNull();
            var createdToken = result.Value!;
            createdToken.IdChangePassword.Should().BeGreaterThan(0).And.NotBe(oldTokenId);
            createdToken.PasswordResetToken.Should().Be(newTokenValue);
            createdToken.PersonId.Should().Be(personId);
            // Verifies that the creation timestamp uses the time provided by the mock provider.
            createdToken.TokenCreatedAt.Should().Be(_mockUtcNow);

            // Verifies persistence: the new token exists, and the old one is removed.
            var verifyOptions = DbContextTestHelper.CreateOptionsForExistingConnection(_connection);
            using (var verifyContext = new ApplicationDbContext(verifyOptions))
            {
                var foundNewToken = await verifyContext.Changepassword.FindAsync(createdToken.IdChangePassword);
                foundNewToken.Should().NotBeNull();
                foundNewToken!.Passwordresettoken.Should().Be(newTokenValue);

                var foundOldToken = await verifyContext.Changepassword.FindAsync(oldTokenId);
                // Asserts the old token associated with the person was deleted.
                foundOldToken.Should().BeNull("Old token should have been deleted by Create method");
            }
        }

        [Fact]
        public async Task GetActiveByTokenAsync_WhenTokenExistsAndIsActive_ShouldReturnToken()
        {
            // Arrange
            var tokenValue = "active-token";
            // Creates a token that expires in the future relative to the fixed test time.
            var efToken = CreateTestEfToken(tokenValue: tokenValue, expiresIn: TimeSpan.FromHours(1));
            _context.Changepassword.Add(efToken);
            await _context.SaveChangesAsync();

            // Act
            // The mock time provider uses the fixed _mockUtcNow established in the constructor.
            var result = await _tokenRepository.GetActiveByTokenAsync(tokenValue);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNull();
            result.Value!.PasswordResetToken.Should().Be(tokenValue);
            result.Value.IdChangePassword.Should().Be(efToken.Idchangepassword);
            result.Value.ResetTokenExpiresAt.Should().Be(efToken.Resettokenexpiresat);
        }

        [Fact]
        public async Task GetActiveByTokenAsync_WhenTokenExistsButExpired_ShouldReturnFailure()
        {
            // Arrange
            var tokenValue = "expired-token";
            // Creates a token that has already expired relative to the fixed test time.
            // Created 1 hour ago, expired 30 minutes ago based on _mockUtcNow.
            var efToken = CreateTestEfToken(tokenValue: tokenValue, createdAt: _mockUtcNow.AddHours(-1), expiresIn: TimeSpan.FromMinutes(30));
            _context.Changepassword.Add(efToken);
            await _context.SaveChangesAsync();

            // Act
            var result = await _tokenRepository.GetActiveByTokenAsync(tokenValue);

            // Assert
            result.Should().NotBeNull();
            result.IsFailure.Should().BeTrue();
            // Checks for the expected error message content indicating expiration.
            result.ErrorMessage.Should().Contain("expired");
        }

        [Fact]
        public async Task GetActiveByTokenAsync_WhenTokenDoesNotExist_ShouldReturnFailure()
        {
            // Arrange
            var nonExistentToken = "no-such-token";

            // Act
            var result = await _tokenRepository.GetActiveByTokenAsync(nonExistentToken);

            // Assert
            result.Should().NotBeNull();
            result.IsFailure.Should().BeTrue();
            // Checks for the expected error message content indicating the token was not found.
            result.ErrorMessage.Should().Contain("invalid");
        }

        [Fact]
        public async Task DeleteTokensByPersonIdAsync_ShouldRemoveAllTokensForPerson()
        {
            // Arrange
            var personId = DEFAULT_TEST_PERSON_ID;
            var otherPersonId = personId + 1;
            // Seeds a second person for isolation testing.
            if (!await _context.Person.AnyAsync(p => p.Idperson == otherPersonId))
            {
                _context.Person.Add(new Infrastructure.Data.Person { Idperson = otherPersonId, Firstname = "Other", Lastname = "TokenUser", Email = "other.token@test.com", Password = "p", Createdat = _mockUtcNow });
                await _context.SaveChangesAsync();
                _context.ChangeTracker.Clear();
            }

            var token1 = CreateTestEfToken(personId: personId, tokenValue: "token-del-1");
            var token2 = CreateTestEfToken(personId: personId, tokenValue: "token-del-2");
            var tokenOther = CreateTestEfToken(personId: otherPersonId, tokenValue: "token-other");
            _context.Changepassword.AddRange(token1, token2, tokenOther);
            await _context.SaveChangesAsync();

            // Verifies the initial state before deletion.
            (await _context.Changepassword.CountAsync(t => t.Personid == personId)).Should().Be(2);
            (await _context.Changepassword.CountAsync(t => t.Personid == otherPersonId)).Should().Be(1);

            // Act
            var result = await _tokenRepository.DeleteTokensByPersonIdAsync(personId);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            // Asserts that the Delete operation returns true upon success.
            result.Value.Should().BeTrue();

            // Verifies persistence: tokens for the target person are gone, others remain.
            (await _context.Changepassword.CountAsync(t => t.Personid == personId)).Should().Be(0);
            // Verifies that the other person's token remains untouched.
            (await _context.Changepassword.CountAsync(t => t.Personid == otherPersonId)).Should().Be(1);
        }

        [Fact]
        public async Task GetById_WhenTokenExists_ShouldReturnToken()
        {
            // Arrange
            var efToken = CreateTestEfToken();
            _context.Changepassword.Add(efToken);
            await _context.SaveChangesAsync();
            var id = efToken.Idchangepassword;

            // Act
            var result = await _tokenRepository.GetById(id);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNull();
            result.Value!.IdChangePassword.Should().Be(id);
            result.Value.PasswordResetToken.Should().Be(efToken.Passwordresettoken);
        }

        [Fact]
        public async Task GetById_WhenTokenDoesNotExist_ShouldReturnFailure()
        {
            // Arrange
            var nonExistentId = 99999;

            // Act
            var result = await _tokenRepository.GetById(nonExistentId);

            // Assert
            result.Should().NotBeNull();
            result.IsFailure.Should().BeTrue();
            result.ErrorMessage.Should().Contain("not found");
        }
    }
}