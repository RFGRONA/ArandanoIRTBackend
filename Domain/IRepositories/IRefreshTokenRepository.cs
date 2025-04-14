using ArandanoIRT_Backend.Domain.Entities;
using ArandanoIRT_Backend.Domain.ValueObjects;

namespace ArandanoIRT_Backend.Domain.IRepositories
{
    /// <summary>
    /// Defines the repository contract specifically for managing <see cref="RefreshTokenEntity"/> instances.
    /// Includes specific operations for token retrieval, cleanup, and revocation,
    /// in addition to the standard CRUD-like operations inherited from <see cref="IRepository{T}"/>.
    /// </summary>
    public interface IRefreshTokenRepository : IRepository<RefreshTokenEntity>
    {
        /// <summary>
        /// Asynchronously retrieves a refresh token entity by its unique token string.
        /// </summary>
        /// <param name="token">The refresh token string to search for.</param>
        /// <returns>
        /// A <see cref="Task{TResult}"/> representing the asynchronous operation, containing a
        /// <see cref="Result{T}"/> which holds the <see cref="RefreshTokenEntity"/> if found,
        /// or a failure result otherwise.
        /// </returns>
        Task<Result<RefreshTokenEntity>> GetByTokenAsync(string token);

        /// <summary>
        /// Asynchronously retrieves all non-revoked refresh tokens associated with a specific user.
        /// </summary>
        /// <param name="personId">The ID of the user whose active tokens are to be retrieved.</param>
        /// <returns>
        /// A <see cref="Task{TResult}"/> representing the asynchronous operation, containing a
        /// <see cref="Result{T}"/> which holds an <see cref="IEnumerable{T}"/> of active <see cref="RefreshTokenEntity"/>
        /// instances if successful, or a failure result otherwise. The list may be empty.
        /// </returns>
        Task<Result<IEnumerable<RefreshTokenEntity>>> GetActiveTokensByPersonIdAsync(int personId);

        /// <summary>
        /// Asynchronously deletes all refresh tokens associated with a specific session identifier.
        /// </summary>
        /// <param name="sessionId">The session identifier whose tokens should be deleted.</param>
        /// <returns>
        /// A <see cref="Task{TResult}"/> representing the asynchronous operation, containing a
        /// <see cref="Result{T}"/> of <c>bool</c> indicating whether the deletion was successful (<c>true</c>)
        /// or failed (<c>false</c>), along with any error information in case of failure.
        /// </returns>
        Task<Result<bool>> DeleteBySessionAsync(long sessionId);

        /// <summary>
        /// Asynchronously deletes all refresh tokens for a specific user that expired before a given date. Used for periodic cleanup.
        /// </summary>
        /// <param name="personId">The ID of the user whose expired tokens should be deleted.</param>
        /// <param name="olderThan">The cutoff timestamp. Tokens expiring before this timestamp will be deleted.</param>
        /// <returns>
        /// A <see cref="Task{TResult}"/> representing the asynchronous operation, containing a
        /// <see cref="Result{T}"/> of <c>bool</c> indicating whether the deletion was successful (<c>true</c>)
        /// or failed (<c>false</c>), along with any error information in case of failure.
        /// </returns>
        Task<Result<bool>> DeleteExpiredTokensAsync(int personId, DateTime olderThan);

        /// <summary>
        /// Asynchronously revokes (marks as invalid) all active refresh tokens associated with a specific session ID.
        /// </summary>
        /// <param name="sessionId">The session ID for which all associated active tokens should be revoked.</param>
        /// <param name="revokedByIp">The IP address performing the revocation action.</param>
        /// <returns>
        /// A <see cref="Task{TResult}"/> representing the asynchronous operation, containing a
        /// <see cref="Result{T}"/> of <c>bool</c> indicating whether the revocation was successful (<c>true</c>)
        /// or failed (<c>false</c>), along with any error information in case of failure.
        /// </returns>
        Task<Result<bool>> RevokeBySessionIdAsync(long sessionId, string revokedByIp);
    }
}