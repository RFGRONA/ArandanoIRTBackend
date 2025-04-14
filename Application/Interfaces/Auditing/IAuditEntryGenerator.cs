using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace ArandanoIRT_Backend.Application.Interfaces.Auditing
{
    /// <summary>
    /// Defines a contract for components responsible for generating audit log entries
    /// based on entity changes tracked by Entity Framework Core.
    /// </summary>
    public interface IAuditEntryGenerator
    {
        /// <summary>
        /// Determines whether this generator can process the specified entity entry for auditing.
        /// </summary>
        /// <param name="entry">The Entity Framework Core <see cref="EntityEntry"/> representing the tracked change.</param>
        /// <returns><c>true</c> if this generator can handle the entry; otherwise, <c>false</c>.</returns>
        bool CanHandle(EntityEntry entry);

        /// <summary>
        /// Generates one or more audit log entries based on the changes in the specified entity entry
        /// and the provided metadata.
        /// </summary>
        /// <param name="entry">The <see cref="EntityEntry"/> containing the details of the entity change (e.g., original and current values).</param>
        /// <param name="metadata">The <see cref="AuditMetadata"/> containing context about the action being audited (user, time, etc.).</param>
        /// <returns>
        /// An <see cref="IEnumerable{T}"/> collection of objects, where each object represents
        /// a distinct audit log entry to be persisted. The specific type of these objects
        /// depends on the implementation.
        /// </returns>
        IEnumerable<object> GenerateEntries(EntityEntry entry, AuditMetadata metadata);
    }
}