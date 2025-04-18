using ArandanoIRT_Backend.Domain.Entities;
using ArandanoIRT_Backend.Domain.IRepositories;
using ArandanoIRT_Backend.Domain.ValueObjects;

namespace ArandanoIRT_Backend.Infrastructure.Repositories
{
    public class DeviceActivationRepository : IDeviceActivationRepository
    {
        public Task<Result<DeviceActivationEntity>> Create(DeviceActivationEntity entity)
        {
            throw new NotImplementedException();
        }

        public Task<Result<bool>> Delete(int id)
        {
            throw new NotImplementedException();
        }

        public Task<Result<IEnumerable<DeviceActivationEntity>>> GetAll()
        {
            throw new NotImplementedException();
        }

        public Task<Result<DeviceActivationEntity>> GetById(int id)
        {
            throw new NotImplementedException();
        }

        public Task<Result<bool>> Update(DeviceActivationEntity entity)
        {
            throw new NotImplementedException();
        }
    }
}
