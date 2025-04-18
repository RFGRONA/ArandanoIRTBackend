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
            // Injects the audit helper service.
            _auditHelper = auditHelper ?? throw new ArgumentNullException(nameof(auditHelper));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        /// <remarks>
        /// This generator specifically handles entities of type <c>Person</c>.
        /// </remarks>
        public bool CanHandle(EntityEntry entry)
        {
            // Handles the Person entity type.
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
            // List to return; may contain 0, 1, or multiple entries (for UPDATE).
            var auditEntries = new List<Auditperson>();

            // Ensures the entity is of the expected type (Person), performing a safety check.
            if (entry.Entity is not Person personEntity) // Uses pattern matching for type check and assignment.
            {
                // Logs a warning if incorrectly called (though CanHandle should prevent this).
                _logger.LogWarning("AuditPersonGenerator called with unexpected entity type: {EntityType}", entry.Entity.GetType().Name);
                return auditEntries; // Returns empty list.
            }

            // Gets IDs using the helper (will be reused).
            var recordId = _auditHelper.GetPrimaryKeyValue(entry);
            var cropId = _auditHelper.GetCropIdValue(entry);

            // Determines action type and populates relevant fields based on the entity's state.
            switch (entry.State)
            {
                case EntityState.Added:
                    _logger.LogInformation("Generating INSERT audit entry for Person.");
                    auditEntries.Add(new Auditperson
                    {
                        Recordid = 0, // Placeholder, real ID does not exist yet.
                        Cropid = cropId, // CropId might be available upon creation.
                        Action = "INSERT",
                        Columnname = "ALL", // For INSERT, indicates the whole entity.
                        Performedat = metadata.PerformedAt,
                        Performedby = metadata.UserId,
                        Performedbyip = metadata.IpAddress,
                        Useragent = metadata.UserAgent
                    });
                    break;

                case EntityState.Deleted:
                    _logger.LogInformation("Generating DELETE audit entry for Person ID: {PersonId}", recordId);
                    auditEntries.Add(new Auditperson
                    {
                        Recordid = recordId ?? 0, // ID of the deleted person.
                        Cropid = cropId, // Original CropId.
                        Action = "DELETE",
                        Columnname = "ALL", // For DELETE, indicates the whole entity.
                        Performedat = metadata.PerformedAt,
                        Performedby = metadata.UserId,
                        Performedbyip = metadata.IpAddress,
                        Useragent = metadata.UserAgent
                    });
                    break;

                case EntityState.Modified:
                    _logger.LogInformation("Generating UPDATE audit entry(s) for Person ID: {PersonId}", recordId);
                    // Iterates through each property of the entity entry.
                    foreach (var property in entry.Properties)
                    {
                        // IGNORE if the property was not modified OR if it is the Password property.
                        if (!property.IsModified || property.Metadata.Name == nameof(Person.Password))
                        {
                            continue; // Skips to the next property.
                        }

                        _logger.LogInformation(" -- Modified column in Person: {ColumnName}", property.Metadata.Name);
                        // Creates a separate audit entry for each modified (non-password) property.
                        auditEntries.Add(new Auditperson
                        {
                            Recordid = recordId ?? 0, // ID of the modified person.
                            Cropid = cropId, // Current CropId (or original, depending on helper logic).
                            Action = "UPDATE",
                            Columnname = property.Metadata.Name, // Name of the specific modified column.
                            // Does not save Old/New Value as per current requirement/design for this audit type.
                            Performedat = metadata.PerformedAt,
                            Performedby = metadata.UserId,
                            Performedbyip = metadata.IpAddress,
                            Useragent = metadata.UserAgent
                        });
                    }
                    break;

                default:
                    // Logs a warning for any unexpected entity states encountered.
                    _logger.LogWarning("Unexpected entity state {EntityState} detected for Person ID: {PersonId} in AuditPersonGenerator.", entry.State, recordId);
                    // Returns empty list for unexpected states.
                    break;
            }

            // Returns the list of Auditperson objects cast to object.
            return auditEntries.Cast<object>();
        }
    }
}