namespace ArandanoIRT_Backend.Domain.Exceptions
{
    /// <summary>
    /// Represents an error that occurs when a requested resource, entity,
    /// or record could not be found in the system or data store.
    /// </summary>
    /// <remarks>
    /// This exception inherits from the base <see cref="Exception"/> class.
    /// </remarks>
    public class NotFoundException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NotFoundException"/> class
        /// with a specified error message that describes the resource that could not be found.
        /// </summary>
        /// <param name="message">The message that describes the requested resource and indicates it was not found.</param>
        public NotFoundException(string message) : base(message)
        {
            // The base Exception constructor handles the message assignment.
        }
    }
}