using ArandanoIRT_Backend.Domain.Entities;
using ArandanoIRT_Backend.Domain.IRepositories;
using ArandanoIRT_Backend.Domain.ValueObjects;

namespace ArandanoIRT_Backend.Infrastructure.Repositories
{
    public class PlantDataRepository : IPlantDataRepository
    {
        public Task<Result<PlantDataEntity>> Create(PlantDataEntity entity)
        {
            throw new NotImplementedException();
        }

        public Task<Result<bool>> Delete(int id)
        {
            throw new NotImplementedException();
        }

        public Task<Result<IEnumerable<PlantDataEntity>>> GetAll()
        {
            throw new NotImplementedException();
        }

        public Task<Result<PlantDataEntity>> GetById(int id)
        {
            throw new NotImplementedException();
        }

        public Task<Result<bool>> Update(PlantDataEntity entity)
        {
            throw new NotImplementedException();
        }
    }
}
