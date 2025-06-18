using ArandanoIRT_Backend.Domain.Entities;
using ArandanoIRT_Backend.Domain.ValueObjects;

namespace ArandanoIRT_Backend.Domain.IRepositories
{
    /// <summary>
    /// Defines the repository contract specifically for managing <see cref="DeviceLogEntity"/> instances.
    /// Inherits standard CRUD-like operations from <see cref="IRepository{T}"/>.
    /// </summary>
    public interface IDeviceLogRepository : IRepository<DeviceLogEntity>
    {
        /// <summary>
        /// Retrieves all log entries for a specific device, ordered by timestamp descending.
        /// </summary>
        /// <param name="deviceId">The ID of the device whose logs to retrieve.</param>
        /// <returns>A Result indicating success and a collection of log entities, or failure.</returns>
        Task<Result<IEnumerable<DeviceLogEntity>>> GetLogsByDeviceIdAsync(int deviceId);
    }
}
