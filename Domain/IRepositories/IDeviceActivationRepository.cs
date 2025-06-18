using ArandanoIRT_Backend.Domain.Entities;
using ArandanoIRT_Backend.Domain.ValueObjects;

namespace ArandanoIRT_Backend.Domain.IRepositories
{
    /// <summary>
    /// Defines the repository contract specifically for managing <see cref="DeviceActivationEntity"/> instances.
    /// Inherits standard CRUD-like operations from <see cref="IRepository{T}"/>.
    /// </summary>
    public interface IDeviceActivationRepository : IRepository<DeviceActivationEntity>
    {
        /// <summary>
        /// Asynchronously retrieves a pending and unexpired device activation entry by its activation code.
        /// </summary>
        /// <param name="activationCode">The unique activation code string.</param>
        /// <returns>A Result containing the found activation entity, or failure if the code is invalid, expired, or not pending.</returns>
        Task<Result<DeviceActivationEntity>> GetByActivationCodeAsync(string activationCode);

        /// <summary>
        /// Asynchronously updates the status and activation timestamp of a specific device activation record.
        /// </summary>
        /// <param name="deviceActivationId">The unique identifier of the device activation record to update.</param>
        /// <param name="newStatusId">The new status ID to set for the device activation record.</param>
        /// <param name="activatedAt">The timestamp (UTC) when the device was activated.</param>
        /// <returns>A Result indicating success or failure of the operation.</returns>
        Task<Result> UpdateStatusAsync(int deviceActivationId, int newStatusId, DateTime activatedAt);
    }
}