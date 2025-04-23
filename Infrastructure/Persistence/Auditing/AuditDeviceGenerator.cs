using ArandanoIRT_Backend.Application.Interfaces.Auditing;
using ArandanoIRT_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore;

namespace ArandanoIRT_Backend.Infrastructure.Persistence.Auditing
{
    /// <summary>
    /// Implements <see cref="IAuditEntryGenerator"/> to create audit trail entries
    /// in the <c>Auditdevice</c> table for changes made to <c>Devicedata</c> entities.
    /// </summary>
    public class AuditDeviceGenerator : IAuditEntryGenerator
    {
        private readonly IAuditHelperService _auditHelper;
        private readonly ILogger<AuditDeviceGenerator> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="AuditDeviceGenerator"/> class.
        /// </summary>
        /// <param name="auditHelper">The helper service for common auditing tasks.</param>
        /// <param name="logger">The logger for recording information and warnings.</param>
        /// <exception cref="ArgumentNullException">Thrown if auditHelper or logger is null.</exception>
        public AuditDeviceGenerator(IAuditHelperService auditHelper, ILogger<AuditDeviceGenerator> logger)
        {
            _auditHelper = auditHelper ?? throw new ArgumentNullException(nameof(auditHelper));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        /// <remarks>
        /// This generator specifically handles entities of type <c>Devicedata</c>.
        /// </remarks>
        public bool CanHandle(EntityEntry entry)
        {
            // Handles the Devicedata entity type.
            return entry.Entity is Devicedata;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Generates a single <c>Auditdevice</c> record for INSERT, UPDATE, or DELETE operations on a <c>Devicedata</c> entity.
        /// Performs row-level auditing ("ALL" column) using the <see cref="IAuditHelperService"/>.
        /// </remarks>
        public IEnumerable<object> GenerateEntries(EntityEntry entry, AuditMetadata metadata)
        {
            // Safety check to ensure the entity is of the expected EF Core/Data context type 'Devicedata'.
            if (entry.Entity is not Devicedata)
            {
                _logger.LogWarning("AuditDeviceGenerator called with unexpected entity type: {EntityType}", entry.Entity.GetType().Name);
                return Enumerable.Empty<object>();
            }

            // Creates the audit database entity instance ('Auditdevice').
            var audit = new Auditdevice
            {
                // Populates common audit metadata.
                Performedat = metadata.PerformedAt,
                Performedby = metadata.UserId,
                Performedbyip = metadata.IpAddress,
                Useragent = metadata.UserAgent,

                // Sets specific audit information for the device change.
                Columnname = "ALL", // Sets column name to "ALL" for row-level auditing.
                Action = entry.State.ToString().ToUpperInvariant(), // Action based on entity state.
                Recordid = _auditHelper.GetPrimaryKeyValue(entry) ?? 0, // Gets the primary key of the Devicedata entity.
                Cropid = _auditHelper.GetCropIdValue(entry)       // Gets the CropId foreign key from the Devicedata entity.
            };

            // Logs the generation attempt.
            _logger.LogInformation("Generating AuditDevice for Devicedata ID: {RecordId}, Action: {Action}",
                                   audit.Recordid, audit.Action);

            // Populates OldValue and NewValue based on the entity state.
            switch (entry.State)
            {
                case EntityState.Added:
                    audit.Oldvalue = null;
                    audit.Newvalue = _auditHelper.SerializePropertyValueDictionary(
                                         _auditHelper.GetValuesDictionary(entry.CurrentValues /*, excludedProps */)
                                     );
                    audit.Recordid = 0; // ID does not exist yet for added entities.
                    break;

                case EntityState.Deleted:
                    audit.Oldvalue = _auditHelper.SerializePropertyValueDictionary(
                                        _auditHelper.GetValuesDictionary(entry.OriginalValues /*, excludedProps */)
                                     );
                    audit.Newvalue = null;
                    // RecordId and CropId were already obtained using the helper.
                    break;

                case EntityState.Modified:
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
                    _logger.LogWarning("Unexpected entity state {EntityState} detected for Devicedata ID: {RecordId} in AuditDeviceGenerator.",
                                       entry.State, audit.Recordid);
                    return Enumerable.Empty<object>(); // Does not generate an audit entry.
            }

            // Returns a list containing the single generated audit entry object.
            return new List<object> { audit };
        }
    }
}