using ArandanoIRT_Backend.Domain.Entities;
using ArandanoIRT_Backend.Domain.ValueObjects;

namespace ArandanoIRT_Backend.Domain.IRepositories
{
    /// <summary>
    /// Defines the repository contract specifically for managing <see cref="StatusEntity"/> instances.
    /// Includes an operation to retrieve statuses by associated table name,
    /// in addition to the standard CRUD-like operations inherited from <see cref="IRepository{T}"/>.
    /// </summary>
    public interface IStatusRepository : IRepository<StatusEntity>
    {
        /// <summary>
        /// Asynchronously retrieves all status entities associated with a specific table name,
        /// likely determined via the <see cref="StatusEntity.TableRelationId"/>.
        /// </summary>
        /// <param name="tableName">The name of the table for which to retrieve associated statuses.</param>
        /// <returns>
        /// A <see cref="Task{TResult}"/> representing the asynchronous operation, containing a
        /// <see cref="Result{T}"/> which holds an <see cref="IEnumerable{T}"/> of relevant <see cref="StatusEntity"/>
        /// instances if successful, or a failure result otherwise. The list may be empty.
        /// </returns>
        Task<Result<IEnumerable<StatusEntity>>> GetStatusesByTableNameAsync(string tableName);
    }
}