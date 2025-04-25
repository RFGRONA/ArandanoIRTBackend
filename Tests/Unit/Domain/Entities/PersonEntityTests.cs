using ArandanoIRT_Backend.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace ArandanoIRT_Backend.Tests.Unit.Domain.Entities
{
    /// <summary>
    /// Contains unit tests for the <see cref="PersonEntity"/> class.
    /// Focuses on constructor validation logic.
    /// </summary>
    public class PersonEntityTests
    {
        // --- Helper Method to Create Valid PersonEntity ---

        /// <summary>
        /// Creates a valid instance of <see cref="PersonEntity"/> with specified or default values.
        /// </summary>
        /// <param name="id">The person ID.</param>
        /// <param name="firstName">The first name.</param>
        /// <param name="lastName">The last name.</param>
        /// <param name="email">The email address.</param>
        /// <param name="password">The hashed password.</param>
        /// <param name="createdAt">The creation timestamp. Defaults to UtcNow if null.</param>
        /// <param name="isAdmin">Flag indicating if the person is an admin.</param>
        /// <param name="allNotifications">Flag indicating if all notifications are enabled.</param>
        /// <param name="cropId">The associated Crop ID (optional).</param>
        /// <returns>A new instance of <see cref="PersonEntity"/>.</returns>
        private static PersonEntity CreateValidPersonEntity(
            int id = 1,
            string firstName = "Test",
            string lastName = "User",
            string email = "test@example.com",
            string password = "hashedpassword",
            // Defaults to UtcNow if null is passed.
            DateTime? createdAt = null,
            bool isAdmin = false,
            bool allNotifications = true,
            int? cropId = null)
        {
            // Ensures CreatedAt is always set, using UtcNow if the parameter is null.
            return new PersonEntity(
                id,
                firstName,
                lastName,
                email,
                password,
                createdAt ?? DateTime.UtcNow,
                isAdmin,
                allNotifications,
                cropId
            );
        }

        /// <summary>
        /// Verifies that the constructor throws ArgumentException when FirstName is null.
        /// </summary>
        [Fact]
        public void Constructor_ShouldThrowArgumentException_WhenFirstNameIsNull()
        {
            // Arrange
            string? nullFirstName = null;

            // Act
            // Uses null-forgiving operator (!) to satisfy compiler for test setup.
            Action act = () => CreateValidPersonEntity(firstName: nullFirstName!);

            // Assert
            act.Should().Throw<ArgumentException>()
               .WithMessage("First name is required. (Parameter 'firstName')");
        }

        /// <summary>
        /// Verifies that the constructor throws ArgumentException when FirstName is empty.
        /// </summary>
        [Fact]
        public void Constructor_ShouldThrowArgumentException_WhenFirstNameIsEmpty()
        {
            // Arrange
            string emptyFirstName = string.Empty;

            // Act
            Action act = () => CreateValidPersonEntity(firstName: emptyFirstName);

            // Assert
            act.Should().Throw<ArgumentException>()
               .WithMessage("First name is required. (Parameter 'firstName')");
        }

        /// <summary>
        /// Verifies that the constructor throws ArgumentException when FirstName is whitespace.
        /// </summary>
        [Fact]
        public void Constructor_ShouldThrowArgumentException_WhenFirstNameIsWhitespace()
        {
            // Arrange
            string whitespaceFirstName = "    ";

            // Act
            Action act = () => CreateValidPersonEntity(firstName: whitespaceFirstName);

            // Assert
            act.Should().Throw<ArgumentException>()
               .WithMessage("First name is required. (Parameter 'firstName')");
        }

        /// <summary>
        /// Verifies that the constructor throws ArgumentException when LastName is null.
        /// </summary>
        [Fact]
        public void Constructor_ShouldThrowArgumentException_WhenLastNameIsNull()
        {
            // Arrange
            string? nullLastName = null;

            // Act
            // Uses null-forgiving operator (!) for test setup.
            Action act = () => CreateValidPersonEntity(lastName: nullLastName!);

            // Assert
            act.Should().Throw<ArgumentException>()
               .WithMessage("Last name is required. (Parameter 'lastName')");
        }

        /// <summary>
        /// Verifies that the constructor throws ArgumentException when LastName is empty.
        /// </summary>
        [Fact]
        public void Constructor_ShouldThrowArgumentException_WhenLastNameIsEmpty()
        {
            // Arrange
            string emptyLastName = string.Empty;

            // Act
            Action act = () => CreateValidPersonEntity(lastName: emptyLastName);

            // Assert
            act.Should().Throw<ArgumentException>()
               .WithMessage("Last name is required. (Parameter 'lastName')");
        }

        /// <summary>
        /// Verifies that the constructor throws ArgumentException when LastName is whitespace.
        /// </summary>
        [Fact]
        public void Constructor_ShouldThrowArgumentException_WhenLastNameIsWhitespace()
        {
            // Arrange
            string whitespaceLastName = "    ";

            // Act
            Action act = () => CreateValidPersonEntity(lastName: whitespaceLastName);

            // Assert
            act.Should().Throw<ArgumentException>()
               .WithMessage("Last name is required. (Parameter 'lastName')");
        }

        /// <summary>
        /// Verifies that the constructor successfully creates an instance
        /// and assigns properties correctly when valid arguments are provided.
        /// </summary>
        [Fact]
        public void Constructor_ShouldCreateInstance_WhenValidArgumentsProvided()
        {
            // Arrange
            int expectedId = 10;
            string expectedFirstName = "Valid";
            string expectedLastName = "Name";
            string expectedEmail = "valid.name@test.com";
            string expectedPassword = "validPasswordHash";
            // Uses a specific time for predictability in assertion.
            DateTime expectedCreatedAt = DateTime.UtcNow.AddMinutes(-5);
            bool expectedIsAdmin = true;
            bool expectedAllNotifications = false;
            int? expectedCropId = 5;

            // Act
            var person = new PersonEntity(
                expectedId,
                expectedFirstName,
                expectedLastName,
                expectedEmail,
                expectedPassword,
                expectedCreatedAt,
                expectedIsAdmin,
                expectedAllNotifications,
                expectedCropId
            );

            // Assert
            person.IdPerson.Should().Be(expectedId);
            person.FirstName.Should().Be(expectedFirstName);
            person.LastName.Should().Be(expectedLastName);
            person.Email.Should().Be(expectedEmail);
            person.Password.Should().Be(expectedPassword);
            person.CreatedAt.Should().Be(expectedCreatedAt);
            person.IsAdmin.Should().Be(expectedIsAdmin);
            person.AllNotifications.Should().Be(expectedAllNotifications);
            person.CropId.Should().Be(expectedCropId);
            // Properties not explicitly set in the constructor are expected to have default values (e.g., null for nullable DateTime).
            person.UpdatedAt.Should().BeNull();
            person.LastLoginAt.Should().BeNull();
            person.LastPasswordChangeAt.Should().BeNull();
        }

        /// <summary>
        /// Verifies that the constructor works correctly when the optional CropId is null.
        /// </summary>
        [Fact]
        public void Constructor_ShouldCreateInstance_WhenOptionalCropIdIsNull()
        {
            // Arrange
            int expectedId = 11;
            string expectedFirstName = "Another";
            string expectedLastName = "User";
            string expectedEmail = "another.user@test.com";
            string expectedPassword = "anotherPasswordHash";
            DateTime expectedCreatedAt = DateTime.UtcNow.AddDays(-1);
            bool expectedIsAdmin = false;
            bool expectedAllNotifications = true;
            // Explicitly sets CropId to null for this test case.
            int? nullCropId = null;

            // Act
            var person = new PersonEntity(
                expectedId,
                expectedFirstName,
                expectedLastName,
                expectedEmail,
                expectedPassword,
                expectedCreatedAt,
                expectedIsAdmin,
                expectedAllNotifications,
                // Passes null for the optional CropId argument.
                nullCropId
            );

            // Assert
            person.IdPerson.Should().Be(expectedId);
            person.FirstName.Should().Be(expectedFirstName);
            person.LastName.Should().Be(expectedLastName);
            // Verifies that the CropId property is correctly assigned as null.
            person.CropId.Should().BeNull();
        }
    }
}