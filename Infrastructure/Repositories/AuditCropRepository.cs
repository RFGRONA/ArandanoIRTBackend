using ArandanoIRT_Backend.Domain.Entities;
using ArandanoIRT_Backend.Domain.IRepositories;
using ArandanoIRT_Backend.Domain.ValueObjects;

namespace ArandanoIRT_Backend.Infrastructure.Repositories
{
    public class AuditCropRepository : IAuditCropRepository
    {
        public Task<Result<AuditCropEntity>> Create(AuditCropEntity entity)
        {
            throw new NotImplementedException();
        }

        public Task<Result<bool>> Delete(int id)
        {
            throw new NotImplementedException();
        }

        public Task<Result<IEnumerable<AuditCropEntity>>> GetAll()
        {
            throw new NotImplementedException();
        }

        public Task<Result<AuditCropEntity>> GetById(int id)
        {
            throw new NotImplementedException();
        }

        public Task<Result<bool>> Update(AuditCropEntity entity)
        {
            throw new NotImplementedException();
        }
    }
}
