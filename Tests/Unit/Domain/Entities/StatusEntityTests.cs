using ArandanoIRT_Backend.Domain.Entities; 
using FluentAssertions;
using Xunit;

namespace ArandanoIRT_Backend.Tests.Unit.Domain.Entities
{
    /// <summary>
    /// Contains unit tests for the <see cref="StatusEntity"/> class.
    /// Focuses on constructor validation logic.
    /// </summary>
    public class StatusEntityTests
    {
        /// <summary>
        /// Verifies that the constructor throws an ArgumentException
        /// when the status name provided is null.
        /// </summary>
        [Fact]
        public void Constructor_ShouldThrowArgumentException_WhenNameStatusIsNull()
        {
            // Arrange
            int validId = 1;
            // Explicitly uses nullable type for clarity in the test setup.
            string? nullName = null;

            // Act
            // Defines the action of attempting to create the entity with invalid (null) input.
            // Uses the null-forgiving operator (!) because an exception is expected before use.
            Action act = () => new StatusEntity(validId, nullName!);

            // Assert
            // Checks that the action throws the correct exception type.
            // Verifies the specific exception message content.
            act.Should().Throw<ArgumentException>()
               .WithMessage("The status name is required. (Parameter 'nameStatus')");
        }

        /// <summary>
        /// Verifies that the constructor throws an ArgumentException
        /// when the status name provided is empty.
        /// </summary>
        [Fact]
        public void Constructor_ShouldThrowArgumentException_WhenNameStatusIsEmpty()
        {
            // Arrange
            int validId = 1;
            string emptyName = string.Empty;

            // Act
            Action act = () => new StatusEntity(validId, emptyName);

            // Assert
            act.Should().Throw<ArgumentException>()
               .WithMessage("The status name is required. (Parameter 'nameStatus')");
        }

        /// <summary>
        /// Verifies that the constructor throws an ArgumentException
        /// when the status name provided consists only of whitespace.
        /// </summary>
        [Fact]
        public void Constructor_ShouldThrowArgumentException_WhenNameStatusIsWhitespace()
        {
            // Arrange
            int validId = 1;
            string whitespaceName = "    ";

            // Act
            Action act = () => new StatusEntity(validId, whitespaceName);

            // Assert
            act.Should().Throw<ArgumentException>()
               .WithMessage("The status name is required. (Parameter 'nameStatus')");
        }

        /// <summary>
        /// Verifies that the constructor successfully creates an instance
        /// and assigns properties correctly when valid arguments are provided.
        /// </summary>
        [Fact]
        public void Constructor_ShouldCreateInstance_WhenValidArgumentsProvided()
        {
            // Arrange
            int expectedId = 1;
            string expectedName = "Active";
            int? expectedTableRelationId = 10;

            // Act
            // Creates the entity instance with valid test data.
            var statusEntity = new StatusEntity(expectedId, expectedName, expectedTableRelationId);

            // Assert
            // Verifies that all properties are assigned the expected values.
            statusEntity.IdStatus.Should().Be(expectedId);
            statusEntity.NameStatus.Should().Be(expectedName);
            statusEntity.TableRelationId.Should().Be(expectedTableRelationId);
            // The TableRelation navigation property is expected to be null when the entity is created directly via constructor.
            statusEntity.TableRelation.Should().BeNull();
        }

        /// <summary>
        /// Verifies that the constructor successfully creates an instance
        /// when the optional TableRelationId is null.
        /// </summary>
        [Fact]
        public void Constructor_ShouldCreateInstance_WhenTableRelationIdIsNull()
        {
            // Arrange
            int expectedId = 2;
            string expectedName = "Pending";
            int? nullTableRelationId = null; // Explicitly setting optional parameter to null.

            // Act
            var statusEntity = new StatusEntity(expectedId, expectedName, nullTableRelationId);

            // Assert
            statusEntity.IdStatus.Should().Be(expectedId);
            statusEntity.NameStatus.Should().Be(expectedName);
            statusEntity.TableRelationId.Should().BeNull(); // Verifies the optional ID is null.
            statusEntity.TableRelation.Should().BeNull(); // Navigation property should also be null.
        }
    }
}