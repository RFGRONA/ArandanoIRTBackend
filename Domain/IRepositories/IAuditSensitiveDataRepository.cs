using ArandanoIRT_Backend.Domain.Entities;

namespace ArandanoIRT_Backend.Domain.IRepositories
{
    /// <summary>
    /// Defines the repository contract specifically for managing <see cref="AuditSensitiveDataEntity"/> instances.
    /// Inherits standard CRUD-like operations from <see cref="IRepository{T}"/>.
    /// </summary>
    public interface IAuditSensitiveDataRepository : IRepository<AuditSensitiveDataEntity>
    {
    }
}