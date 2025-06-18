using ArandanoIRT_Backend.Domain.Entities;
using ArandanoIRT_Backend.Domain.ValueObjects;

namespace ArandanoIRT_Backend.Domain.IRepositories
{
    /// <summary>
    /// Defines the repository contract specifically for managing <see cref="ThermalDataEntity"/> instances.
    /// Inherits standard CRUD-like operations from <see cref="IRepository{T}"/>.
    /// </summary>
    public interface IThermalDataRepository : IRepository<ThermalDataEntity>
    {
        /// <summary>
        /// Retrieves all thermal data records associated with a specific plant, ordered by recording time descending.
        /// </summary>
        /// <param name="plantId">The ID of the plant whose thermal data to retrieve.</param>
        /// <returns>A <see cref="Result{T}"/> indicating success and a collection of <see cref="ThermalDataEntity"/>, or failure.</returns>
        Task<Result<IEnumerable<ThermalDataEntity>>> GetAllByPlantIdAsync(int plantId);

        /// <summary>
        /// Retrieves the latest thermal data record for a specific plant.
        /// </summary>
        /// <param name="plantId">The ID of the plant whose latest thermal data to retrieve.</param>
        /// <returns>A <see cref="Result{T}"/> indicating success and the latest <see cref="ThermalDataEntity"/>, or failure if not found.</returns>
        Task<Result<ThermalDataEntity>> GetLatestByPlantIdAsync(int plantId);
    }
}
