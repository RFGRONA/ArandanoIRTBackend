using ArandanoIRT_Backend.Domain.Entities;
using ArandanoIRT_Backend.Domain.IRepositories;
using ArandanoIRT_Backend.Domain.ValueObjects;

namespace ArandanoIRT_Backend.Infrastructure.Repositories
{
    public class AuditDeviceRepository : IAuditDeviceRepository
    {
        public Task<Result<AuditDeviceEntity>> Create(AuditDeviceEntity entity)
        {
            throw new NotImplementedException();
        }

        public Task<Result<bool>> Delete(int id)
        {
            throw new NotImplementedException();
        }

        public Task<Result<IEnumerable<AuditDeviceEntity>>> GetAll()
        {
            throw new NotImplementedException();
        }

        public Task<Result<AuditDeviceEntity>> GetById(int id)
        {
            throw new NotImplementedException();
        }

        public Task<Result<bool>> Update(AuditDeviceEntity entity)
        {
            throw new NotImplementedException();
        }
    }
}
