using ArandanoIRT_Backend.Domain.ValueObjects;

namespace ArandanoIRT_Backend.Domain.IRepositories
{
    /// <summary>
    /// Defines a generic contract for repository operations for a specific entity type <typeparamref name="T"/>.
    /// Provides standard asynchronous CRUD-like methods using the Result pattern.
    /// </summary>
    /// <typeparam name="T">The type of the entity managed by the repository. Must be a class.</typeparam>
    public interface IRepository<T> where T : class
    {
        /// <summary>
        /// Asynchronously retrieves an entity by its unique identifier.
        /// </summary>
        /// <param name="id">The unique identifier of the entity to retrieve.</param>
        /// <returns>
        /// A <see cref="Task{TResult}"/> representing the asynchronous operation, containing a
        /// <see cref="Result{T}"/> which holds the entity if found, or a failure result otherwise.
        /// </returns>
        Task<Result<T>> GetById(int id);

        /// <summary>
        /// Asynchronously retrieves all entities of type <typeparamref name="T"/>.
        /// </summary>
        /// <returns>
        /// A <see cref="Task{TResult}"/> representing the asynchronous operation, containing a
        /// <see cref="Result{T}"/> which holds an <see cref="IEnumerable{T}"/> of entities if successful,
        /// or a failure result otherwise.
        /// </returns>
        Task<Result<IEnumerable<T>>> GetAll();

        /// <summary>
        /// Asynchronously creates a new entity in the data store.
        /// </summary>
        /// <param name="entity">The entity instance to create.</param>
        /// <returns>
        /// A <see cref="Task{TResult}"/> representing the asynchronous operation, containing a
        /// <see cref="Result{T}"/> which holds the created entity (potentially with updated properties like ID)
        /// if successful, or a failure result otherwise.
        /// </returns>
        Task<Result<T>> Create(T entity);

        /// <summary>
        /// Asynchronously updates an existing entity in the data store.
        /// </summary>
        /// <param name="entity">The entity instance with updated values.</param>
        /// <returns>
        /// A <see cref="Task{TResult}"/> representing the asynchronous operation, containing a
        /// <see cref="Result{T}"/> of <c>bool</c> indicating whether the update was successful (<c>true</c>)
        /// or failed (<c>false</c>), along with any error information in case of failure.
        /// </returns>
        Task<Result<bool>> Update(T entity);

        /// <summary>
        /// Asynchronously deletes an entity from the data store by its unique identifier.
        /// </summary>
        /// <param name="id">The unique identifier of the entity to delete.</param>
        /// <returns>
        /// A <see cref="Task{TResult}"/> representing the asynchronous operation, containing a
        /// <see cref="Result{T}"/> of <c>bool</c> indicating whether the deletion was successful (<c>true</c>)
        /// or failed (<c>false</c>), along with any error information in case of failure.
        /// </returns>
        Task<Result<bool>> Delete(int id);
    }
}