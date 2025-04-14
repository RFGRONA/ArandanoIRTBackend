namespace ArandanoIRT_Backend.Domain.Exceptions
{
    /// <summary>
    /// Represents a general error that occurs when input data or an object's state
    /// fails one or more validation checks.
    /// </summary>
    /// <remarks>
    /// This exception inherits from the base <see cref="Exception"/> class.
    /// Consider using more specific exceptions (e.g., ArgumentException, ArgumentNullException, ArgumentOutOfRangeException)
    /// or custom exceptions inheriting from this one for more detailed error handling where appropriate.
    /// </remarks>
    public class ValidationException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ValidationException"/> class
        /// with a specified error message that describes the validation failure.
        /// </summary>
        /// <param name="message">The message that describes the validation error.</param>
        public ValidationException(string message) : base(message)
        {
            // The base Exception constructor handles the message assignment.
        }
    }
}