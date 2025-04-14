using ArandanoIRT_Backend.Domain.Entities;
using ArandanoIRT_Backend.Domain.ValueObjects;

namespace ArandanoIRT_Backend.Domain.IRepositories
{
    /// <summary>
    /// Defines the repository contract specifically for managing <see cref="CropInvitationEntity"/> instances.
    /// Includes operations for retrieving active invitations by code and marking them as used,
    /// in addition to the standard CRUD-like operations inherited from <see cref="IRepository{T}"/>.
    /// </summary>
    public interface ICropInvitationRepository : IRepository<CropInvitationEntity>
    {
        /// <summary>
        /// Asynchronously retrieves an active (not used, not expired) crop invitation by its access code.
        /// </summary>
        /// <param name="accessCode">The unique access code of the invitation.</param>
        /// <returns>
        /// A <see cref="Task{TResult}"/> representing the asynchronous operation, containing a
        /// <see cref="Result{T}"/> which holds the <see cref="CropInvitationEntity"/> if found and active,
        /// or a failure result otherwise.
        /// </returns>
        Task<Result<CropInvitationEntity>> GetActiveByCodeAsync(string accessCode);

        /// <summary>
        /// Asynchronously marks a specific crop invitation as used by associating a user ID and potentially updating its status.
        /// </summary>
        /// <param name="invitationId">The ID of the invitation to mark as used.</param>
        /// <param name="userId">The ID of the person who used the invitation.</param>
        /// <returns>
        /// A <see cref="Task{TResult}"/> representing the asynchronous operation, containing a
        /// <see cref="Result{T}"/> of <c>bool</c> indicating whether the operation was successful (<c>true</c>)
        /// or failed (<c>false</c>), along with any error information in case of failure.
        /// </returns>
        Task<Result<bool>> MarkAsUsedAsync(int invitationId, int userId);
    }
}