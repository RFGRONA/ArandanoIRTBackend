using ArandanoIRT_Backend.Domain.Entities;
using ArandanoIRT_Backend.Domain.ValueObjects;

namespace ArandanoIRT_Backend.Domain.IRepositories
{
    /// <summary>
    /// Defines the repository contract specifically for managing <see cref="DeviceTokenEntity"/> instances.
    /// Inherits standard CRUD-like operations from <see cref="IRepository{T}"/>.
    /// </summary>
    public interface IDeviceTokenRepository : IRepository<DeviceTokenEntity>
    {
        /// <summary>
        /// Asynchronously retrieves a device token by its token string value.
        /// </summary>
        /// <param name="token">The unique token string.</param>
        /// <returns>A Result containing the found token entity, or failure if not found or an error occurs.</returns>
        Task<Result<DeviceTokenEntity>> GetByTokenAsync(string token);

        /// <summary>
        /// Asynchronously retrieves all device tokens associated with a specific device identifier.
        /// </summary>
        /// <param name="deviceId">The ID of the device.</param>
        /// <returns>A Result containing a collection of device token entities, or failure if an error occurs. Returns an empty collection if no tokens are found.</returns>
        Task<Result<IEnumerable<DeviceTokenEntity>>> GetByDeviceIdAsync(int deviceId);

        /// <summary>
        /// Asynchronously revokes a device token identified by its token string.
        /// </summary>
        /// <param name="token">The unique token string to revoke.</param>
        /// <param name="revokedAt">The timestamp when the token was revoked.</param>
        /// <param name="revokedByIp">The IP address from which the revocation request originated.</param>
        /// <returns>A Result indicating if the revocation was successful.</returns>
        Task<Result<bool>> RevokeByTokenAsync(string token, DateTime revokedAt, string? revokedByIp);

        /// <summary>
        /// Asynchronously revokes all active device tokens associated with a specific device identifier.
        /// </summary>
        /// <param name="deviceId">The ID of the device whose tokens are to be revoked.</param>
        /// <param name="revokedAt">The timestamp when the tokens were revoked.</param>
        /// <param name="revokedByIp">The IP address from which the revocation request originated.</param>
        /// <returns>A Result indicating if the revocation of tokens for the device was successful.</returns>
        Task<Result<bool>> RevokeByDeviceIdAsync(int deviceId, DateTime revokedAt, string? revokedByIp);

        /// <summary>
        /// Asynchronously deletes all device tokens associated with a specific device identifier.
        /// </summary>
        /// <param name="deviceId">The ID of the device whose tokens are to be deleted.</param>
        /// <returns>A Result indicating if the deletion of tokens for the device was successful.</returns>
        Task<Result<bool>> DeleteByDeviceIdAsync(int deviceId);
    }
}