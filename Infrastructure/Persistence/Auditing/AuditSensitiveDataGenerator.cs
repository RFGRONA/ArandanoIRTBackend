using ArandanoIRT_Backend.Application.Interfaces.Auditing; 
using ArandanoIRT_Backend.Infrastructure.Data; 
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Serilog;

namespace ArandanoIRT_Backend.Infrastructure.Persistence.Auditing
{
    /// <summary>
    /// Implements <see cref="IAuditEntryGenerator"/> to create audit trail entries
    /// in the <c>Auditsensitivedata</c> table for actions performed on sensitive entities
    /// like tokens, passwords, invitations, and activations.
    /// This generator focuses on logging the action itself, not the old/new values.
    /// </summary>
    public class AuditSensitiveDataGenerator : IAuditEntryGenerator
    {
        // Static logger instance specific to this generator.
        private readonly Serilog.ILogger _logger = Log.ForContext<AuditSensitiveDataGenerator>();

        // Note: This generator does not inject IAuditHelperService or ILogger via constructor,
        // relying on local helper methods and a static logger instance.

        /// <inheritdoc/>
        /// <remarks>
        /// This generator handles entities deemed sensitive, such as
        /// <c>Changepassword</c>, <c>Refreshtoken</c>, <c>Devicetoken</c>,
        /// <c>Cropinvitation</c>, and <c>Deviceactivation</c>.
        /// </remarks>
        public bool CanHandle(EntityEntry entry)
        {
            // Checks if the entity type is one mapped to AuditSensitiveData.
            return entry.Entity is Changepassword ||
                   entry.Entity is Refreshtoken ||
                   entry.Entity is Devicetoken ||
                   entry.Entity is Cropinvitation ||
                   entry.Entity is Deviceactivation;
            // Add other types here if necessary.
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Generates a single <c>Auditsensitivedata</c> record for operations on supported sensitive entity types.
        /// It logs the table name, record ID, action type, CropId (if found), and common metadata.
        /// It performs row-level auditing ("ALL" column) and crucially **does not** record OldValue or NewValue for sensitivity reasons.
        /// Uses local helper methods <c>GetPrimaryKeyValue</c> and <c>GetCropIdValue</c> for ID retrieval.
        /// </remarks>
        public IEnumerable<object> GenerateEntries(EntityEntry entry, AuditMetadata metadata)
        {
            // Performs a check intended to ensure the entity is one of the expected types handled by CanHandle.
            if (entry.Entity is not Changepassword &&
                entry.Entity is not Refreshtoken &&
                entry.Entity is not Devicetoken &&
                entry.Entity is not Cropinvitation &&
                entry.Entity is not Deviceactivation)
            {
                _logger.Warning("AuditSensitiveDataGenerator potentially called with unexpected type (check logic): {EntityType}", entry.Entity.GetType().Name);
                return Enumerable.Empty<object>(); // Returns an empty collection.
            }

            // Creates the specific audit entity instance ('Auditsensitivedata').
            var audit = new Auditsensitivedata();

            // Assigns common metadata.
            audit.Performedat = metadata.PerformedAt;
            audit.Performedby = metadata.UserId;
            audit.Performedbyip = metadata.IpAddress;
            audit.Useragent = metadata.UserAgent;

            // Sets specific data for AuditSensitiveData.
            audit.Tablename = entry.Metadata.GetTableName() ?? entry.Entity.GetType().Name; // Name of the affected table.
            audit.Columnname = "ALL"; // Indicates the action applies to the whole entity.
            audit.Action = entry.State.ToString().ToUpperInvariant(); // INSERT, UPDATE, DELETE.

            // Gets the ID of the affected record (PK) using the local helper.
            audit.Recordid = GetPrimaryKeyValue(entry);

            // Attempts to get CropId if available directly on the entity using the local helper.
            audit.Cropid = GetCropIdValue(entry); // Will be null if CropId property doesn't exist on the entity.

            // Logs the generation attempt.
            _logger.Information("Generating AuditSensitiveData for {TableName} ID: {RecordId}, Action: {Action}",
                                audit.Tablename, audit.Recordid, audit.Action);

            // OldValue and NewValue are intentionally not set for sensitive data audits.

            // Returns a list containing the single generated audit entry object.
            return new List<object> { audit };
        }

        /// <summary>
        /// Local helper method to retrieve the primary key value as an integer from an EntityEntry.
        /// </summary>
        /// <param name="entry">The EntityEntry representing the tracked entity.</param>
        /// <returns>The integer primary key value, or 0 if not found, not an integer, or composite.</returns>
        private int GetPrimaryKeyValue(EntityEntry entry)
        {
            var primaryKey = entry.Metadata.FindPrimaryKey();
            if (primaryKey != null && primaryKey.Properties.Count == 1)
            {
                var pkProperty = primaryKey.Properties.First();
                // Tries CurrentValue first, even for DELETE state.
                var pkValue = entry.Property(pkProperty.Name)?.CurrentValue;

                if (pkValue is int intValue)
                {
                    return intValue;
                }
                // If used for DELETE and CurrentValue failed (e.g., entity not fully loaded), try OriginalValue.
                if (entry.State == EntityState.Deleted)
                {
                    pkValue = entry.Property(pkProperty.Name)?.OriginalValue;
                    if (pkValue is int intValueDeleted) return intValueDeleted;
                }

                _logger.Warning("Could not get PK of type INT for {TableName}. PK Name: {PKName}, PK Type: {PKType}",
                                entry.Metadata.GetTableName() ?? "<Unknown>", pkProperty.Name, pkValue?.GetType().Name ?? "null");
            }
            else // Handles null or composite PK
            {
                _logger.Warning("Could not determine unique PK for {TableName}.", entry.Metadata.GetTableName() ?? "<Unknown>");
            }

            // If RecordId in the audit table can be 0 for error/INSERT cases, return 0.
            return 0;
        }

        /// <summary>
        /// Local helper method to retrieve the CropId value as a nullable integer from an EntityEntry.
        /// </summary>
        /// <param name="entry">The EntityEntry representing the tracked entity.</param>
        /// <returns>The integer CropId value if found, otherwise null.</returns>
        private int? GetCropIdValue(EntityEntry entry)
        {
            // Tries to get the property "CropId" or "Cropid" (case-insensitive).
            var cropIdProp = entry.Properties.FirstOrDefault(p => p.Metadata.Name.Equals("CropId", StringComparison.OrdinalIgnoreCase) || p.Metadata.Name.Equals("Cropid", StringComparison.OrdinalIgnoreCase));

            if (cropIdProp != null)
            {
                // Tries CurrentValue first.
                if (cropIdProp.CurrentValue is int cropId)
                {
                    return cropId;
                }
                // For DELETE, try OriginalValue if CurrentValue is null (less likely for FK).
                if (entry.State == EntityState.Deleted && cropIdProp.OriginalValue is int originalCropId)
                {
                    return originalCropId;
                }
            }

            // Attempt to get it from a navigation property if not direct (more complex, avoid if possible in interceptor).
            // Example: if (entry.Entity is DeviceData dd && dd.Crop != null) return dd.Crop.Idcrop;

            // Returns null if the property is not found or its value is not an integer.
            return null;
        }
    }
}