using ArandanoIRT_Backend.Domain.Entities;
using ArandanoIRT_Backend.Domain.IRepositories;
using ArandanoIRT_Backend.Domain.ValueObjects;

namespace ArandanoIRT_Backend.Infrastructure.Repositories
{
    public class PlantStateHistoryRepository : IPlantStateHistoryRepository
    {
        public Task<Result<PlantStateHistoryEntity>> Create(PlantStateHistoryEntity entity)
        {
            throw new NotImplementedException();
        }

        public Task<Result<bool>> Delete(int id)
        {
            throw new NotImplementedException();
        }

        public Task<Result<IEnumerable<PlantStateHistoryEntity>>> GetAll()
        {
            throw new NotImplementedException();
        }

        public Task<Result<PlantStateHistoryEntity>> GetById(int id)
        {
            throw new NotImplementedException();
        }

        public Task<Result<bool>> Update(PlantStateHistoryEntity entity)
        {
            throw new NotImplementedException();
        }
    }
}
