using ArandanoIRT_Backend.Domain.Entities;
using ArandanoIRT_Backend.Domain.ValueObjects;

namespace ArandanoIRT_Backend.Domain.IRepositories
{
    /// <summary>
    /// Defines the repository contract specifically for managing <see cref="SensorDataEntity"/> instances.
    /// Inherits standard CRUD-like operations from <see cref="IRepository{T}"/>.
    /// </summary>
    public interface ISensorDataRepository : IRepository<SensorDataEntity>
    {
        /// <summary>
        /// Retrieves all sensor data records associated with a specific plant, ordered by recording time descending.
        /// </summary>
        /// <param name="plantId">The ID of the plant whose sensor data to retrieve.</param>
        /// <returns>A <see cref="Result{T}"/> indicating success and a collection of <see cref="SensorDataEntity"/>, or failure.</returns>
        Task<Result<IEnumerable<SensorDataEntity>>> GetAllByPlantIdAsync(int plantId);

        /// <summary>
        /// Retrieves the latest sensor data record for a specific plant.
        /// </summary>
        /// <param name="plantId">The ID of the plant whose latest sensor data to retrieve.</param>
        /// <returns>A <see cref="Result{T}"/> indicating success and the latest <see cref="SensorDataEntity"/>, or failure if not found.</returns>
        Task<Result<SensorDataEntity>> GetLatestByPlantIdAsync(int plantId);
    }
}
