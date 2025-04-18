using ArandanoIRT_Backend.Domain.Entities;
using ArandanoIRT_Backend.Domain.IRepositories;
using ArandanoIRT_Backend.Domain.ValueObjects;

namespace ArandanoIRT_Backend.Infrastructure.Repositories
{
    public class AuditSensitiveDataRepository : IAuditSensitiveDataRepository
    {
        public Task<Result<AuditSensitiveDataEntity>> Create(AuditSensitiveDataEntity entity)
        {
            throw new NotImplementedException();
        }

        public Task<Result<bool>> Delete(int id)
        {
            throw new NotImplementedException();
        }

        public Task<Result<IEnumerable<AuditSensitiveDataEntity>>> GetAll()
        {
            throw new NotImplementedException();
        }

        public Task<Result<AuditSensitiveDataEntity>> GetById(int id)
        {
            throw new NotImplementedException();
        }

        public Task<Result<bool>> Update(AuditSensitiveDataEntity entity)
        {
            throw new NotImplementedException();
        }
    }
}
