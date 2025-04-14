using ArandanoIRT_Backend.Domain.Entities;
using ArandanoIRT_Backend.Domain.IRepositories;
using ArandanoIRT_Backend.Domain.ValueObjects;

namespace ArandanoIRT_Backend.Infrastructure.Repositories
{
    public class TableRelationRepository : ITableRelationRepository
    {
        public Task<Result<TableRelationEntity>> Create(TableRelationEntity entity)
        {
            throw new NotImplementedException();
        }

        public Task<Result<bool>> Delete(int id)
        {
            throw new NotImplementedException();
        }

        public Task<Result<IEnumerable<TableRelationEntity>>> GetAll()
        {
            throw new NotImplementedException();
        }

        public Task<Result<TableRelationEntity>> GetById(int id)
        {
            throw new NotImplementedException();
        }

        public Task<Result<bool>> Update(TableRelationEntity entity)
        {
            throw new NotImplementedException();
        }
    }
}
