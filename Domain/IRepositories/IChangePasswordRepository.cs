using ArandanoIRT_Backend.Domain.Entities;
using ArandanoIRT_Backend.Domain.ValueObjects; 

namespace ArandanoIRT_Backend.Domain.IRepositories
{
    /// <summary>
    /// Defines the repository contract specifically for managing <see cref="ChangePasswordEntity"/> instances.
    /// Includes operations for retrieving active tokens and cleaning up tokens by user,
    /// in addition to the standard CRUD-like operations inherited from <see cref="IRepository{T}"/>.
    /// </summary>
    public interface IChangePasswordRepository : IRepository<ChangePasswordEntity>
    {
        /// <summary>
        /// Asynchronously retrieves an active (not expired and not used) change password entity by its reset token.
        /// </summary>
        /// <param name="token">The password reset token string to search for.</param>
        /// <returns>
        /// A <see cref="Task{TResult}"/> representing the asynchronous operation, containing a
        /// <see cref="Result{T}"/> which holds the <see cref="ChangePasswordEntity"/> if found and active,
        /// or a failure result otherwise.
        /// </returns>
        Task<Result<ChangePasswordEntity>> GetActiveByTokenAsync(string token);

        /// <summary>
        /// Asynchronously deletes all change password tokens associated with a specific person.
        /// Useful for cleanup or when a new token is requested, ensuring only one active token exists per user.
        /// </summary>
        /// <param name="personId">The ID of the person whose password reset tokens should be deleted.</param>
        /// <returns>
        /// A <see cref="Task{TResult}"/> representing the asynchronous operation, containing a
        /// <see cref="Result{T}"/> of <c>bool</c> indicating whether the deletion was successful (<c>true</c>)
        /// or failed (<c>false</c>), along with any error information in case of failure.
        /// </returns>
        Task<Result<bool>> DeleteTokensByPersonIdAsync(int personId);
    }
}