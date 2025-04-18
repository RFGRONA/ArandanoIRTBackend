using ArandanoIRT_Backend.Domain.Entities;
using ArandanoIRT_Backend.Domain.IRepositories;
using ArandanoIRT_Backend.Domain.ValueObjects;

namespace ArandanoIRT_Backend.Infrastructure.Repositories
{
    public class SensorDataRepository : ISensorDataRepository
    {
        public Task<Result<SensorDataEntity>> Create(SensorDataEntity entity)
        {
            throw new NotImplementedException();
        }

        public Task<Result<bool>> Delete(int id)
        {
            throw new NotImplementedException();
        }

        public Task<Result<IEnumerable<SensorDataEntity>>> GetAll()
        {
            throw new NotImplementedException();
        }

        public Task<Result<SensorDataEntity>> GetById(int id)
        {
            throw new NotImplementedException();
        }

        public Task<Result<bool>> Update(SensorDataEntity entity)
        {
            throw new NotImplementedException();
        }
    }
}
