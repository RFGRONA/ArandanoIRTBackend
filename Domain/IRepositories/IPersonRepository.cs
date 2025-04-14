using ArandanoIRT_Backend.Domain.Entities;
using ArandanoIRT_Backend.Domain.ValueObjects;

namespace ArandanoIRT_Backend.Domain.IRepositories
{
    /// <summary>
    /// Defines the repository contract specifically for managing <see cref="PersonEntity"/> instances.
    /// Includes operations for retrieving users by email and updating passwords,
    /// in addition to the standard CRUD-like operations inherited from <see cref="IRepository{T}"/>.
    /// </summary>
    public interface IPersonRepository : IRepository<PersonEntity>
    {
        /// <summary>
        /// Asynchronously retrieves a person entity by their unique email address.
        /// </summary>
        /// <param name="email">The email address to search for.</param>
        /// <returns>
        /// A <see cref="Task{TResult}"/> representing the asynchronous operation, containing a
        /// <see cref="Result{T}"/> which holds the <see cref="PersonEntity"/> if found,
        /// or a failure result otherwise.
        /// </returns>
        Task<Result<PersonEntity>> GetByEmailAsync(string email);

        /// <summary>
        /// Asynchronously updates the password hash and the last password change timestamp for a specified person.
        /// </summary>
        /// <param name="personId">The ID of the person entity whose password is to be updated.</param>
        /// <param name="newPasswordHash">The new, securely hashed password to store.</param>
        /// <param name="changeTimestamp">The UTC timestamp when the password change occurred.</param>
        /// <returns>
        /// A <see cref="Task{TResult}"/> representing the asynchronous operation, containing a
        /// <see cref="Result{T}"/> of <c>bool</c> indicating whether the update was successful (<c>true</c>)
        /// or failed (<c>false</c>), along with any error information in case of failure.
        /// </returns>
        Task<Result<bool>> UpdatePasswordAsync(int personId, string newPasswordHash, DateTime changeTimestamp);
    }
}