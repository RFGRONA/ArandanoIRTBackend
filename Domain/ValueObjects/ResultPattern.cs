namespace ArandanoIRT_Backend.Domain.ValueObjects
{
    /// <summary>
    /// Represents the outcome of an operation, indicating either success or failure.
    /// Contains an error message if the operation failed.
    /// </summary>
    public class Result
    {
        /// <summary>
        /// Gets a value indicating whether the operation was successful.
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>
        /// Gets the error message associated with a failed operation.
        /// Is <see cref="string.Empty"/> if the operation was successful.
        /// </summary>
        public string ErrorMessage { get; }

        /// <summary>
        /// Gets a value indicating whether the operation failed. Returns the opposite of <see cref="IsSuccess"/>.
        /// </summary>
        public bool IsFailure => !IsSuccess;

        /// <summary>
        /// Initializes a new instance of the <see cref="Result"/> class.
        /// Protected constructor enforces consistency between success status and error message presence.
        /// </summary>
        /// <param name="isSuccess">Indicates if the operation was successful.</param>
        /// <param name="errorMessage">The error message if the operation failed; otherwise, <see cref="string.Empty"/>.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if <paramref name="isSuccess"/> is true but <paramref name="errorMessage"/> is not null or empty,
        /// or if <paramref name="isSuccess"/> is false but <paramref name="errorMessage"/> is null or empty.
        /// </exception>
        protected Result(bool isSuccess, string errorMessage)
        {
            // Ensures consistency: success implies no error message.
            if (isSuccess && !string.IsNullOrEmpty(errorMessage))
                throw new InvalidOperationException("A successful result cannot have an error message.");

            // Ensures consistency: failure implies an error message.
            if (!isSuccess && string.IsNullOrEmpty(errorMessage))
                throw new InvalidOperationException("A failed result requires an error message.");

            IsSuccess = isSuccess;
            ErrorMessage = errorMessage ?? string.Empty; // Ensure ErrorMessage is never null
        }

        /// <summary>
        /// Creates a successful <see cref="Result"/> instance with no error message.
        /// </summary>
        /// <returns>A new successful <see cref="Result"/>.</returns>
        public static Result Success() => new(true, string.Empty);

        /// <summary>
        /// Creates a failed <see cref="Result"/> instance with the specified error message.
        /// </summary>
        /// <param name="errorMessage">The message describing the failure.</param>
        /// <returns>A new failed <see cref="Result"/> with the given error message.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="errorMessage"/> is null (or becomes empty after check in constructor).</exception>
        public static Result Failure(string errorMessage) => new(false, errorMessage);
    }

    /// <summary>
    /// Represents the outcome of an operation that returns a value upon success.
    /// Inherits from <see cref="Result"/> and includes a <see cref="Value"/> property for the successful result.
    /// </summary>
    /// <typeparam name="T">The type of the value returned upon success.</typeparam>
    public class Result<T> : Result
    {
        /// <summary>
        /// Gets the value returned by the successful operation.
        /// Accessing this property on a failed result may yield the default value for type <typeparamref name="T"/>.
        /// Check <see cref="Result.IsSuccess"/> before accessing.
        /// </summary>
        public T Value { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Result{T}"/> class.
        /// Protected constructor ensures consistency.
        /// </summary>
        /// <param name="value">The value returned by the operation (meaningful only if <paramref name="isSuccess"/> is true).</param>
        /// <param name="isSuccess">Indicates if the operation was successful.</param>
        /// <param name="errorMessage">The error message if the operation failed; otherwise, <see cref="string.Empty"/>.</param>
        protected Result(T value, bool isSuccess, string errorMessage) : base(isSuccess, errorMessage)
        {
            // Assigns the value regardless of success/failure. Caller should check IsSuccess.
            Value = value;
        }

        /// <summary>
        /// Creates a successful <see cref="Result{T}"/> instance with the specified value.
        /// </summary>
        /// <param name="value">The value returned by the successful operation.</param>
        /// <returns>A new successful <see cref="Result{T}"/> containing the value.</returns>
        public static Result<T> Success(T value) => new(value, true, string.Empty);

        /// <summary>
        /// Creates a failed <see cref="Result{T}"/> instance with the specified error message.
        /// Hides the base <see cref="Result.Failure"/> method.
        /// The <see cref="Value"/> property will be initialized to the default value of type <typeparamref name="T"/>.
        /// </summary>
        /// <param name="errorMessage">The message describing the failure.</param>
        /// <returns>A new failed <see cref="Result{T}"/> with the given error message and default value.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="errorMessage"/> is null (or becomes empty after check in base constructor).</exception>
        public static new Result<T> Failure(string errorMessage) => new(default!, false, errorMessage); // Uses default! for the value in case of failure.
    }
}