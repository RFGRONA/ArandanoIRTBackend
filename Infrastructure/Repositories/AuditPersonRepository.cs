using ArandanoIRT_Backend.Domain.Entities;
using ArandanoIRT_Backend.Domain.IRepositories;
using ArandanoIRT_Backend.Domain.ValueObjects;

namespace ArandanoIRT_Backend.Infrastructure.Repositories
{
    public class AuditPersonRepository : IAuditPersonRepository
    {
        public Task<Result<AuditPersonEntity>> Create(AuditPersonEntity entity)
        {
            throw new NotImplementedException();
        }

        public Task<Result<bool>> Delete(int id)
        {
            throw new NotImplementedException();
        }

        public Task<Result<IEnumerable<AuditPersonEntity>>> GetAll()
        {
            throw new NotImplementedException();
        }

        public Task<Result<AuditPersonEntity>> GetById(int id)
        {
            throw new NotImplementedException();
        }

        public Task<Result<bool>> Update(AuditPersonEntity entity)
        {
            throw new NotImplementedException();
        }
    }
}
