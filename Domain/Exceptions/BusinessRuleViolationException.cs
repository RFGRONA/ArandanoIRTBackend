namespace ArandanoIRT_Backend.Domain.Exceptions
{
    /// <summary>
    /// Represents an error that occurs when an operation violates a specific business rule or constraint.
    /// </summary>
    /// <remarks>
    /// This exception inherits from the base <see cref="Exception"/> class.
    /// </remarks>
    public class BusinessRuleViolationException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BusinessRuleViolationException"/> class
        /// with a specified error message that describes the business rule violation.
        /// </summary>
        /// <param name="message">The message that describes the error and the business rule violated.</param>
        public BusinessRuleViolationException(string message) : base(message)
        {
            // The base Exception constructor handles the message assignment.
        }
    }
}