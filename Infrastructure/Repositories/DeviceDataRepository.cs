using ArandanoIRT_Backend.Domain.Entities;
using ArandanoIRT_Backend.Domain.IRepositories;
using ArandanoIRT_Backend.Domain.ValueObjects;

namespace ArandanoIRT_Backend.Infrastructure.Repositories
{
    public class DeviceDataRepository : IDeviceDataRepository
    {
        public Task<Result<DeviceDataEntity>> Create(DeviceDataEntity entity)
        {
            throw new NotImplementedException();
        }

        public Task<Result<bool>> Delete(int id)
        {
            throw new NotImplementedException();
        }

        public Task<Result<IEnumerable<DeviceDataEntity>>> GetAll()
        {
            throw new NotImplementedException();
        }

        public Task<Result<DeviceDataEntity>> GetById(int id)
        {
            throw new NotImplementedException();
        }

        public Task<Result<bool>> Update(DeviceDataEntity entity)
        {
            throw new NotImplementedException();
        }
    }
}
