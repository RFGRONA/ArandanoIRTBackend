using ArandanoIRT_Backend.Application.Interfaces.Auditing;
using ArandanoIRT_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace ArandanoIRT_Backend.Infrastructure.Persistence.Auditing
{
    /// <summary>
    /// Implements <see cref="IAuditEntryGenerator"/> to create audit trail entries
    /// in the <c>Auditdatatable</c> for changes made to various data entities like
    /// <c>Sensordata</c>, <c>Thermaldata</c>, and <c>Plantdata</c>.
    /// </summary>
    public class AuditDataTableGenerator : IAuditEntryGenerator
    {
        private readonly IAuditHelperService _auditHelper;
        private readonly ILogger<AuditDataTableGenerator> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="AuditDataTableGenerator"/> class.
        /// </summary>
        /// <param name="auditHelper">The helper service for common auditing tasks like serialization and ID retrieval.</param>
        /// <param name="logger">The logger for recording information and warnings.</param>
        /// <exception cref="ArgumentNullException">Thrown if auditHelper or logger is null.</exception>
        public AuditDataTableGenerator(IAuditHelperService auditHelper, ILogger<AuditDataTableGenerator> logger)
        {
            _auditHelper = auditHelper ?? throw new ArgumentNullException(nameof(auditHelper));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        /// <remarks>
        /// This generator handles entities of type <c>Sensordata</c>, <c>Thermaldata</c>, and <c>Plantdata</c>,
        /// directing their audit logs to the <c>Auditdatatable</c>.
        /// </remarks>
        public bool CanHandle(EntityEntry entry)
        {
            // Handles entities intended for the AuditDataTable.
            return entry.Entity is Sensordata ||
                   entry.Entity is Thermaldata ||
                   entry.Entity is Plantdata;
            // Add other entity types here if needed.
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Generates a single <c>Auditdatatable</c> record for INSERT, UPDATE, or DELETE operations
        /// on supported entity types (<c>Sensordata</c>, <c>Thermaldata</c>, <c>Plantdata</c>).
        /// Performs row-level auditing ("ALL" column) using the <see cref="IAuditHelperService"/>.
        /// </remarks>
        public IEnumerable<object> GenerateEntries(EntityEntry entry, AuditMetadata metadata)
        {
            // Performs a check intended to ensure the entity is one of the expected types.
            if (entry.Entity is not Sensordata &&
                entry.Entity is not Thermaldata &&
                entry.Entity is not Plantdata)
            {
                _logger.LogWarning("AuditDataTableGenerator potentially called with unexpected type (check logic): {EntityType}", entry.Entity.GetType().Name);
                return Enumerable.Empty<object>();
            }

            // Creates the audit database entity instance ('Auditdatatable').
            var audit = new Auditdatatable();

            // Populates common audit metadata.
            audit.Performedat = metadata.PerformedAt;
            audit.Performedby = metadata.UserId;
            audit.Performedbyip = metadata.IpAddress;
            audit.Useragent = metadata.UserAgent;

            // Sets table-specific audit information.
            audit.Tablename = entry.Metadata.GetTableName() ?? entry.Entity.GetType().Name; // Gets table name from EF metadata or falls back to class name.
            audit.Columnname = "ALL"; // Sets column name to "ALL" for row-level auditing.
            audit.Action = entry.State.ToString().ToUpperInvariant(); // Action based on entity state (e.g., "ADDED", "MODIFIED", "DELETED").
            audit.Recordid = _auditHelper.GetPrimaryKeyValue(entry) ?? 0; // Retrieves the primary key of the affected record.
            audit.Cropid = _auditHelper.GetCropIdValue(entry); // Retrieves the associated CropId, if available.

            // Logs the generation attempt.
            _logger.LogInformation("Generating AuditDataTable for {TableName} ID: {RecordId}, Action: {Action}",
                                   audit.Tablename, audit.Recordid, audit.Action);

            // Populates OldValue and NewValue based on the entity state.
            switch (entry.State)
            {
                case EntityState.Added:
                    audit.Oldvalue = null;
                    // Serializes the current entity state as the NewValue.
                    audit.Newvalue = _auditHelper.SerializePropertyValueDictionary(
                                         _auditHelper.GetValuesDictionary(entry.CurrentValues /*, excludedProps */)
                                     );
                    audit.Recordid = 0; // Resets RecordId placeholder as ID does not exist yet.
                    break;

                case EntityState.Deleted:
                    // Serializes the original entity state as the OldValue.
                    audit.Oldvalue = _auditHelper.SerializePropertyValueDictionary(
                                         _auditHelper.GetValuesDictionary(entry.OriginalValues /*, excludedProps */)
                                     );
                    audit.Newvalue = null;
                    // RecordId and CropId were already obtained using the helper.
                    break;

                case EntityState.Modified:
                    // Serializes both original and current states.
                    audit.Oldvalue = _auditHelper.SerializePropertyValueDictionary(
                                        _auditHelper.GetValuesDictionary(entry.OriginalValues /*, excludedProps */)
                                     );
                    audit.Newvalue = _auditHelper.SerializePropertyValueDictionary(
                                         _auditHelper.GetValuesDictionary(entry.CurrentValues /*, excludedProps */)
                                     );
                    // RecordId and CropId were already obtained using the helper.
                    break;

                default:
                    // Logs a warning for unexpected entity states.
                    _logger.LogWarning("Unexpected entity state {EntityState} detected for {TableName} ID: {RecordId} in AuditDataTableGenerator.",
                                       entry.State, audit.Tablename, audit.Recordid);
                    return Enumerable.Empty<object>(); // Does not generate an audit entry.
            }

            // Returns a list containing the single generated audit entry object.
            return new List<object> { audit };
        }
    }
}