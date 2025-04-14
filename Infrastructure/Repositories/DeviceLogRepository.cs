using ArandanoIRT_Backend.Domain.Entities;
using ArandanoIRT_Backend.Domain.IRepositories;
using ArandanoIRT_Backend.Domain.ValueObjects;

namespace ArandanoIRT_Backend.Infrastructure.Repositories
{
    public class DeviceLogRepository : IDeviceLogRepository
    {
        public Task<Result<DeviceLogEntity>> Create(DeviceLogEntity entity)
        {
            throw new NotImplementedException();
        }

        public Task<Result<bool>> Delete(int id)
        {
            throw new NotImplementedException();
        }

        public Task<Result<IEnumerable<DeviceLogEntity>>> GetAll()
        {
            throw new NotImplementedException();
        }

        public Task<Result<DeviceLogEntity>> GetById(int id)
        {
            throw new NotImplementedException();
        }

        public Task<Result<bool>> Update(DeviceLogEntity entity)
        {
            throw new NotImplementedException();
        }
    }
}
