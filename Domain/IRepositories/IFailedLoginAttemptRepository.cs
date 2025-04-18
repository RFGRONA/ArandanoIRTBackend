using ArandanoIRT_Backend.Domain.Entities;
using ArandanoIRT_Backend.Domain.ValueObjects;

namespace ArandanoIRT_Backend.Domain.IRepositories
{
    /// <summary>
    /// Defines the repository contract specifically for managing <see cref="FailedLoginAttemptEntity"/> instances.
    /// Includes operations for retrieving recent attempts by user or IP,
    /// in addition to the standard CRUD-like operations inherited from <see cref="IRepository{T}"/>.
    /// </summary>
    public interface IFailedLoginAttemptRepository : IRepository<FailedLoginAttemptEntity>
    {
        /// <summary>
        /// Asynchronously retrieves recent failed login attempts for a specific person recorded since a given timestamp.
        /// </summary>
        /// <param name="personId">The ID of the person whose failed attempts are to be retrieved.</param>
        /// <param name="since">The UTC timestamp indicating the start of the period to check (exclusive or inclusive depends on implementation).</param>
        /// <returns>
        /// A <see cref="Task{TResult}"/> representing the asynchronous operation, containing a
        /// <see cref="Result{T}"/> which holds an <see cref="IEnumerable{T}"/> of <see cref="FailedLoginAttemptEntity"/>
        /// if successful, or a failure result otherwise. The list may be empty if no recent attempts are found.
        /// </returns>
        Task<Result<IEnumerable<FailedLoginAttemptEntity>>> GetRecentAttemptsAsync(int personId, DateTime since);

        /// <summary>
        /// Asynchronously retrieves recent failed login attempts originating from a specific IP address since a given timestamp.
        /// </summary>
        /// <param name="ipAddress">The IP address from which the failed attempts originated.</param>
        /// <param name="since">The UTC timestamp indicating the start of the period to check (exclusive or inclusive depends on implementation).</param>
        /// <returns>
        /// A <see cref="Task{TResult}"/> representing the asynchronous operation, containing a
        /// <see cref="Result{T}"/> which holds an <see cref="IEnumerable{T}"/> of <see cref="FailedLoginAttemptEntity"/>
        /// if successful, or a failure result otherwise. The list may be empty if no recent attempts are found from this IP.
        /// </returns>
        Task<Result<IEnumerable<FailedLoginAttemptEntity>>> GetRecentAttemptsByIpAsync(string ipAddress, DateTime since);
    }
}