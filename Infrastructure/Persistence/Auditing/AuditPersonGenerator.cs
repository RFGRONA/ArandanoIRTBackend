using ArandanoIRT_Backend.Application.Interfaces.Auditing;
using ArandanoIRT_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace ArandanoIRT_Backend.Infrastructure.Persistence.Auditing
{
    /// <summary>
    /// Implements <see cref="IAuditEntryGenerator"/> to create audit trail entries
    /// in the <c>Auditperson</c> table for changes made to <c>Person</c> entities.
    /// </summary>
    public class AuditPersonGenerator : IAuditEntryGenerator
    {
        private readonly IAuditHelperService _auditHelper;
        private readonly ILogger<AuditPersonGenerator> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="AuditPersonGenerator"/> class.
        /// </summary>
        /// <param name="auditHelper">The helper service for common auditing tasks.</param>
        /// <param name="logger">The logger for recording information and warnings.</param>
        /// <exception cref="ArgumentNullException">Thrown if auditHelper or logger is null.</exception>
        public AuditPersonGenerator(IAuditHelperService auditHelper, ILogger<AuditPersonGenerator> logger)
        {
            _auditHelper = auditHelper ?? throw new ArgumentNullException(nameof(auditHelper));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        /// <remarks>
        /// This generator specifically handles entities of type <c>Person</c>.
        /// </remarks>
        public bool CanHandle(EntityEntry entry)
        {
            return entry.Entity is Person;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Generates <c>Auditperson</c> records based on the entity state:
        /// - <c>Added</c>: Generates one entry with "ALL" as ColumnName.
        /// - <c>Deleted</c>: Generates one entry with "ALL" as ColumnName.
        /// - <c>Modified</c>: Generates one entry for *each* modified property (excluding "Password").
        ///   The <c>ColumnName</c> is set to the modified property's name. Old/New values are *not* recorded in this implementation.
        /// Uses <see cref="IAuditHelperService"/> for retrieving IDs.
        /// </remarks>
        public IEnumerable<object> GenerateEntries(EntityEntry entry, AuditMetadata metadata)
        {
            var auditEntries = new List<Auditperson>();

            // Safety check using pattern matching
            if (entry.Entity is not Person personEntity)
            {
                _logger.LogWarning("AuditPersonGenerator called with unexpected entity type: {EntityType}", entry.Entity.GetType().Name);
                return auditEntries; // Return empty list
            }

            var recordId = _auditHelper.GetPrimaryKeyValue(entry);
            var cropId = _auditHelper.GetCropIdValue(entry);

            switch (entry.State)
            {
                case EntityState.Added:
                    _logger.LogInformation("Generating INSERT audit entry for Person.");
                    // Use helper to create the entry
                    auditEntries.Add(CreateBaseAuditEntry(metadata, 0, cropId, "INSERT", "ALL"));
                    break;

                case EntityState.Deleted:
                    _logger.LogInformation("Generating DELETE audit entry for Person ID: {PersonId}", recordId);
                    // Use helper to create the entry
                    auditEntries.Add(CreateBaseAuditEntry(metadata, recordId ?? 0, cropId, "DELETE", "ALL"));
                    break;

                case EntityState.Modified:
                    _logger.LogInformation("Generating UPDATE audit entry(s) for Person ID: {PersonId}", recordId);
                    // Iterate through modified properties
                    foreach (var property in entry.Properties)
                    {
                        // Skip unmodified properties and the Password property
                        if (!property.IsModified || property.Metadata.Name == nameof(Person.Password))
                        {
                            continue;
                        }

                        _logger.LogInformation(" -- Modified column in Person: {ColumnName}", property.Metadata.Name);
                        // Use helper to create an entry for each modified property
                        auditEntries.Add(CreateBaseAuditEntry(metadata, recordId ?? 0, cropId, "UPDATE", property.Metadata.Name));
                    }
                    break;

                default:
                    _logger.LogWarning("Unexpected entity state {EntityState} detected for Person ID: {PersonId} in AuditPersonGenerator.", entry.State, recordId);
                    // Returns empty list for unexpected states.
                    break;
            }

            return auditEntries.Cast<object>();
        }

        /// <summary>
        /// Creates a base Auditperson object populated with common metadata and provided details.
        /// </summary>
        /// <param name="metadata">Common audit metadata (user, time, IP, etc.).</param>
        /// <param name="recordId">The primary key ID of the affected record.</param>
        /// <param name="cropId">The CropId associated with the record.</param>
        /// <param name="action">The action performed (INSERT, UPDATE, DELETE).</param>
        /// <param name="columnName">The name of the column affected ("ALL" for INSERT/DELETE, specific name for UPDATE).</param>
        /// <returns>A new <see cref="Auditperson"/> object.</returns>
        private Auditperson CreateBaseAuditEntry(AuditMetadata metadata, int recordId, int? cropId, string action, string columnName)
        {
            return new Auditperson
            {
                Recordid = recordId,
                Cropid = cropId,
                Action = action,
                Columnname = columnName,
                Performedat = metadata.PerformedAt,
                Performedby = metadata.UserId,
                Performedbyip = metadata.IpAddress,
                Useragent = metadata.UserAgent
                // Oldvalue and Newvalue are intentionally omitted based on the original implementation's logic.
            };
        }
    }
}