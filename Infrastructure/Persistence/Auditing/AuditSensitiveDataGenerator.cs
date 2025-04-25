using ArandanoIRT_Backend.Application.Interfaces.Auditing; 
using ArandanoIRT_Backend.Infrastructure.Data; 
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace ArandanoIRT_Backend.Infrastructure.Persistence.Auditing
{
    /// <summary>
    /// Implements <see cref="IAuditEntryGenerator"/> to create audit trail entries
    /// in the <c>Auditsensitivedata</c> table for actions performed on sensitive entities
    /// like tokens, passwords, invitations, and activations.
    /// This generator focuses on logging the action itself, not the old/new values.
    /// </summary>
    public class AuditSensitiveDataGenerator(ILogger<AuditSensitiveDataGenerator> logger) : IAuditEntryGenerator
    {
        /// <summary>
        /// Logger instance for logging audit generation events.
        /// </summary>
        private readonly ILogger<AuditSensitiveDataGenerator> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

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
            // Redundant check (already covered by CanHandle), but acts as a safeguard.
            // Consider simplifying if CanHandle is always called first reliably.
            if (!(entry.Entity is Changepassword ||
                  entry.Entity is Refreshtoken ||
                  entry.Entity is Devicetoken ||
                  entry.Entity is Cropinvitation ||
                  entry.Entity is Deviceactivation))
            {
                _logger.LogWarning("AuditSensitiveDataGenerator potentially called with unexpected type (check logic): {EntityType}", entry.Entity.GetType().Name);
                return Enumerable.Empty<object>(); // Returns an empty collection.
            }

            var audit = new Auditsensitivedata
            {
                // Common metadata
                Performedat = metadata.PerformedAt,
                Performedby = metadata.UserId,
                Performedbyip = metadata.IpAddress,
                Useragent = metadata.UserAgent,

                // Specific data
                Tablename = entry.Metadata.GetTableName() ?? entry.Entity.GetType().Name,
                Columnname = "ALL",
                Action = entry.State.ToString().ToUpperInvariant(),
                Recordid = GetPrimaryKeyValue(entry), // Use refactored helper
                Cropid = GetCropIdValue(entry) // Use local helper
            };

            _logger.LogInformation("Generating AuditSensitiveData for {TableName} ID: {RecordId}, Action: {Action}",
                                   audit.Tablename, audit.Recordid, audit.Action);

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

            // Guard Clause 1: No Primary Key found
            if (primaryKey == null)
            {
                LogWarningNoOrCompositePK(entry, "No primary key found");
                return 0;
            }

            // Guard Clause 2: Composite Primary Key found
            if (primaryKey.Properties.Count != 1)
            {
                LogWarningNoOrCompositePK(entry, "Composite primary key found");
                return 0;
            }

            // --- We know we have a single primary key property here ---
            var pkProperty = primaryKey.Properties[0];
            var pkPropertyEntry = entry.Property(pkProperty.Name);

            // Try CurrentValue first
            object? currentValue = pkPropertyEntry?.CurrentValue;
            if (currentValue is int currentIntValue)
            {
                return currentIntValue; // Found valid PK int value
            }

            // Fallback: Try OriginalValue only if state is Deleted AND CurrentValue failed
            if (entry.State == EntityState.Deleted)
            {
                object? originalValue = pkPropertyEntry?.OriginalValue;
                if (originalValue is int originalIntValue)
                {
                    return originalIntValue; // Found valid PK int value in OriginalValue
                }
            }

            // If we reached here, no valid integer PK could be determined. Log why.
            // Determine which value caused the failure (usually CurrentValue, unless OriginalValue was attempted)
            object? failedValue = (entry.State == EntityState.Deleted && !(currentValue is int))
                                   ? pkPropertyEntry?.OriginalValue // Log about original if it was checked and failed
                                   : currentValue;                 // Otherwise log about current

            LogWarningPrimaryKeyNotInt(entry, pkProperty.Name, failedValue);
            return 0; // Return default value on failure
        }

        /// <summary>
        /// Local helper method to retrieve the CropId value as a nullable integer from an EntityEntry.
        /// </summary>
        /// <param name="entry">The EntityEntry representing the tracked entity.</param>
        /// <returns>The integer CropId value if found, otherwise null.</returns>
        private static int? GetCropIdValue(EntityEntry entry)
        {
            // Tries to get the property "CropId" or "Cropid" (case-insensitive).
            var cropIdProp = entry.Properties.FirstOrDefault(p => p.Metadata.Name.Equals("CropId", StringComparison.OrdinalIgnoreCase)); // Simplified check

            if (cropIdProp != null)
            {
                // Prioritize CurrentValue, fallback to OriginalValue if Current is null or state is Deleted
                var value = (entry.State == EntityState.Deleted)
                               ? cropIdProp.OriginalValue ?? cropIdProp.CurrentValue // Prefer original if deleted, but take current if original is null somehow
                               : cropIdProp.CurrentValue ?? cropIdProp.OriginalValue; // Prefer current, fallback to original if null

                if (value is int cropId)
                {
                    return cropId;
                }
            }
            return null; // Returns null if the property is not found or its value is not an integer.
        }

        // --- Private Logging Helper Methods ---

        private void LogWarningNoOrCompositePK(EntityEntry entry, string reason)
        {
            _logger.LogWarning("Could not determine unique PK for {TableName}. Reason: {Reason}.",
                               entry.Metadata.GetTableName() ?? entry.Entity.GetType().Name, // Use entity type as fallback for table name
                               reason);
        }

        private void LogWarningPrimaryKeyNotInt(EntityEntry entry, string pkName, object? pkValue)
        {
            _logger.LogWarning("Could not get PK of type INT for {TableName}. PK Name: {PKName}, PK Value: {PKValue}, PK Type: {PKType}",
                               entry.Metadata.GetTableName() ?? entry.Entity.GetType().Name,
                               pkName,
                               pkValue ?? "null", // Show "null" explicitly if value is null
                               pkValue?.GetType().Name ?? "null");
        }
    }
}