using ArandanoIRT_Backend.Domain.Entities;
using ArandanoIRT_Backend.Domain.IRepositories;
using ArandanoIRT_Backend.Domain.ValueObjects;

namespace ArandanoIRT_Backend.Infrastructure.Repositories
{
    public class AuditSystemTableRepository : IAuditSystemTableRepository
    {
        public Task<Result<AuditSystemTableEntity>> Create(AuditSystemTableEntity entity)
        {
            throw new NotImplementedException();
        }

        public Task<Result<bool>> Delete(int id)
        {
            throw new NotImplementedException();
        }

        public Task<Result<IEnumerable<AuditSystemTableEntity>>> GetAll()
        {
            throw new NotImplementedException();
        }

        public Task<Result<AuditSystemTableEntity>> GetById(int id)
        {
            throw new NotImplementedException();
        }

        public Task<Result<bool>> Update(AuditSystemTableEntity entity)
        {
            throw new NotImplementedException();
        }
    }
}
