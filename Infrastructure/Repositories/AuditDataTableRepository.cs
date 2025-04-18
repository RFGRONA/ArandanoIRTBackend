using ArandanoIRT_Backend.Domain.Entities;
using ArandanoIRT_Backend.Domain.IRepositories;
using ArandanoIRT_Backend.Domain.ValueObjects;

namespace ArandanoIRT_Backend.Infrastructure.Repositories
{
    public class AuditDataTableRepository : IAuditDataTableRepository
    {
        public Task<Result<AuditDataTableEntity>> Create(AuditDataTableEntity entity)
        {
            throw new NotImplementedException();
        }

        public Task<Result<bool>> Delete(int id)
        {
            throw new NotImplementedException();
        }

        public Task<Result<IEnumerable<AuditDataTableEntity>>> GetAll()
        {
            throw new NotImplementedException();
        }

        public Task<Result<AuditDataTableEntity>> GetById(int id)
        {
            throw new NotImplementedException();
        }

        public Task<Result<bool>> Update(AuditDataTableEntity entity)
        {
            throw new NotImplementedException();
        }
    }
}
