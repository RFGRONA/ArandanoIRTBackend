using ArandanoIRT_Backend.Domain.ValueObjects; 
using FluentAssertions;
using Xunit;

namespace ArandanoIRT_Backend.Tests.Unit.Domain.ValueObjects
{
    /// <summary>
    /// Contains unit tests for the non-generic <see cref="Result"/> class.
    /// </summary>
    public class ResultTests
    {
        /// <summary>
        /// Verifies that Result.Success() creates a successful Result instance.
        /// </summary>
        [Fact]
        public void Success_ShouldReturnSuccessfulResult()
        {
            // Act
            var result = Result.Success();

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.IsFailure.Should().BeFalse();
            result.ErrorMessage.Should().BeEmpty();
        }

        /// <summary>
        /// Verifies that Result.Failure() creates a failed Result instance with the correct error message.
        /// </summary>
        [Fact]
        public void Failure_ShouldReturnFailedResult_WithErrorMessage()
        {
            // Arrange
            string expectedError = "Something went wrong";

            // Act
            var result = Result.Failure(expectedError);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.IsFailure.Should().BeTrue();
            result.ErrorMessage.Should().Be(expectedError);
        }

        /// <summary>
        /// Verifies that Result.Failure() throws InvalidOperationException if the error message is null or empty.
        /// The factory method prevents creating a Failure state without a meaningful error.
        /// </summary>
        /// <param name="invalidErrorMessage">The invalid error message (null or empty).</param>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Failure_ShouldThrowException_WhenErrorMessageIsNullOrEmpty(string? invalidErrorMessage)
        {
            // Act
            // Defines the action of calling Failure with invalid input.
            Action act = () => Result.Failure(invalidErrorMessage!);

            // Assert
            // Verifies that the factory method throws the expected exception.
            act.Should().Throw<InvalidOperationException>()
               .WithMessage("A failed result requires an error message.");
        }
    }

    /// <summary>
    /// Contains unit tests for the generic <see cref="Result{T}"/> class.
    /// </summary>
    public class ResultTTests
    {
        /// <summary>
        /// Simple class for testing reference type values within Result{T}.
        /// </summary>
        private class TestData { public string Name { get; set; } = "Test"; }

        /// <summary>
        /// Verifies that Result{T}.Success(value) creates a successful Result instance
        /// with the correct value for a reference type.
        /// </summary>
        [Fact]
        public void Success_ShouldReturnSuccessfulResult_WithReferenceTypeValue()
        {
            // Arrange
            var expectedValue = new TestData { Name = "Sample Data" };

            // Act
            var result = Result<TestData>.Success(expectedValue);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.IsFailure.Should().BeFalse();
            result.ErrorMessage.Should().BeEmpty();
            result.Value.Should().BeSameAs(expectedValue); // Checks for reference equality
        }

        /// <summary>
        /// Verifies that Result{T}.Success(value) creates a successful Result instance
        /// with the correct value for a value type (int).
        /// </summary>
        [Fact]
        public void Success_ShouldReturnSuccessfulResult_WithValueTypeValue()
        {
            // Arrange
            int expectedValue = 123;

            // Act
            var result = Result<int>.Success(expectedValue);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.IsFailure.Should().BeFalse();
            result.ErrorMessage.Should().BeEmpty();
            result.Value.Should().Be(expectedValue); // Checks for value equality
        }

        /// <summary>
        /// Verifies that Result{T}.Failure(errorMessage) creates a failed Result instance
        /// with the correct error message and the default value (null) for a reference type.
        /// </summary>
        [Fact]
        public void Failure_ShouldReturnFailedResult_WithErrorMessageAndDefaultReferenceValue()
        {
            // Arrange
            string expectedError = "Failed to retrieve TestData";

            // Act
            var result = Result<TestData>.Failure(expectedError);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.IsFailure.Should().BeTrue();
            result.ErrorMessage.Should().Be(expectedError);
            result.Value.Should().BeNull(); // Default for reference types is null
        }

        /// <summary>
        /// Verifies that Result{T}.Failure(errorMessage) creates a failed Result instance
        /// with the correct error message and the default value (0) for a value type (int).
        /// </summary>
        [Fact]
        public void Failure_ShouldReturnFailedResult_WithErrorMessageAndDefaultValueTypeValue()
        {
            // Arrange
            string expectedError = "Failed to calculate integer";

            // Act
            var result = Result<int>.Failure(expectedError);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.IsFailure.Should().BeTrue();
            result.ErrorMessage.Should().Be(expectedError);
            result.Value.Should().Be(default(int)); // Default for int is 0
        }

        /// <summary>
        /// Verifies that Result{T}.Failure() throws InvalidOperationException if the error message is null or empty.
        /// This check happens in the base class logic invoked by the generic factory method.
        /// </summary>
        /// <param name="invalidErrorMessage">The invalid error message (null or empty).</param>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void FailureT_ShouldThrowException_WhenErrorMessageIsNullOrEmpty(string? invalidErrorMessage)
        {
            // Act
            // Defines the action using TestData as the generic type parameter T.
            Action act = () => Result<TestData>.Failure(invalidErrorMessage!);

            // Assert
            // Verifies that the factory method throws the expected exception due to invalid error message.
            act.Should().Throw<InvalidOperationException>()
              .WithMessage("A failed result requires an error message.");
        }
    }
}