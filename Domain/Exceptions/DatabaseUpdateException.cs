namespace ArandanoIRT_Backend.Domain.Exceptions
{
    /// <summary>
    /// Represents an error that occurs when a database update operation (e.g., insert, update, delete) fails
    /// unexpectedly or results in an inconsistent state.
    /// </summary>
    /// <remarks>
    /// This exception inherits from the base <see cref="Exception"/> class.
    /// Consider adding inner exception details in more complex scenarios if needed.
    /// </remarks>
    public class DatabaseUpdateException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DatabaseUpdateException"/> class
        /// with a specified error message that describes the database update failure.
        /// </summary>
        /// <param name="message">The message that describes the error encountered during the database update.</param>
        public DatabaseUpdateException(string message) : base(message)
        {
            // The base Exception constructor handles the message assignment.
        }
    }
}