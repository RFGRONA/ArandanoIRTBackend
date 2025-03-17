using ArandanoIRT_Backend.Domain.ValueObjetcts;

namespace ArandanoIRT_Backend.Domain.IRepositories
{
    public interface IRepository<T> where T : class
    {
        Task<Result<T>> GetById(int id);
        Task<Result<IEnumerable<T>>> GetAll();
        Task<Result<T>> Create(T entity);
        Task<Result<bool>> Update(T entity);
        Task<Result<bool>> Delete(int id);
    }
}
