using ArandanoIRT_Backend.Domain.Entities;
using ArandanoIRT_Backend.Domain.ValueObjects;

namespace ArandanoIRT_Backend.Domain.IRepositories
{
    /// <summary>
    /// Defines the repository contract specifically for managing <see cref="CropEntity"/> instances.
    /// Inherits standard CRUD-like operations from <see cref="IRepository{T}"/>.
    /// </summary>
    public interface ICropRepository : IRepository<CropEntity>
    {
        /// <summary>
        /// Retrieves a single crop entity by its unique name.
        /// </summary>
        /// <param name="name">The name of the crop to retrieve.</param>
        /// <returns>
        /// A Task representing the asynchronous operation, containing a Result object.
        /// The Result is successful and contains the <see cref="CropEntity"/> if found;
        /// otherwise, the Result is a failure.
        /// </returns>
        Task<Result<CropEntity>> GetByNameAsync(string name);
    }
}