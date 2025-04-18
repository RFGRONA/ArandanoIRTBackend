using ArandanoIRT_Backend.Domain.Entities;
using ArandanoIRT_Backend.Domain.IRepositories;
using ArandanoIRT_Backend.Domain.ValueObjects;

namespace ArandanoIRT_Backend.Infrastructure.Repositories
{
    public class DeviceTokenRepository : IDeviceTokenRepository
    {
        public Task<Result<DeviceTokenEntity>> Create(DeviceTokenEntity entity)
        {
            throw new NotImplementedException();
        }

        public Task<Result<bool>> Delete(int id)
        {
            throw new NotImplementedException();
        }

        public Task<Result<IEnumerable<DeviceTokenEntity>>> GetAll()
        {
            throw new NotImplementedException();
        }

        public Task<Result<DeviceTokenEntity>> GetById(int id)
        {
            throw new NotImplementedException();
        }

        public Task<Result<bool>> Update(DeviceTokenEntity entity)
        {
            throw new NotImplementedException();
        }
    }
}
