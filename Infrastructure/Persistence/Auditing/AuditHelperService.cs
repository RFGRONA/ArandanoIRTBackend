using ArandanoIRT_Backend.Application.Interfaces.Auditing;
using ArandanoIRT_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace ArandanoIRT_Backend.Infrastructure.Persistence.Auditing
{
    /// <summary>
    /// Implements <see cref="IAuditHelperService"/> providing utility methods
    /// commonly used during the generation of audit log entries, such as
    /// primary key retrieval, foreign key retrieval (CropId), and property value serialization.
    /// </summary>
    public class AuditHelperService : IAuditHelperService
    {
        private readonly ILogger<AuditHelperService> _logger;

        // Assume Person class definition exists for nameof(Person.Password)
        public class Person { public string? Password { get; set; } }

        /// <summary>
        /// Defines a set of common sensitive property names (case-insensitive)
        /// to be excluded by default during the serialization of entity values for auditing.
        /// </summary>
        private static readonly HashSet<string> DefaultExcludedProperties = new(StringComparer.OrdinalIgnoreCase)
        {
             nameof(Person.Password) // Excludes the password hash by default.
        };

        /// <summary>
        /// Shared, pre-configured JSON serializer options optimized for audit logging.
        /// Configuration includes: no indentation, ignoring null values, handling cycles, and converting enums to strings.
        /// </summary>
        private static readonly JsonSerializerOptions _jsonSerializerOptions = new()
        {
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            Converters = { new JsonStringEnumConverter() }
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="AuditHelperService"/> class.
        /// </summary>
        /// <param name="logger">The logger for recording information and warnings related to helper operations.</param>
        /// <exception cref="ArgumentNullException">Thrown if logger is null.</exception>
        public AuditHelperService(ILogger<AuditHelperService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        /// <remarks>
        /// This implementation attempts to find a single primary key property of type <see cref="int"/>.
        /// It prioritizes the <c>CurrentValue</c> but falls back to <c>OriginalValue</c> for <see cref="EntityState.Deleted"/> entities.
        /// It logs warnings and returns <c>null</c> if no PK is found, if the PK is composite, if the PK property is not an <see cref="int"/>,
        /// or if the PK value cannot be determined.
        /// </remarks>
        public int? GetPrimaryKeyValue(EntityEntry entry)
        {
            var primaryKey = entry.Metadata.FindPrimaryKey();

            // Guard Clause 1: No Primary Key found
            if (primaryKey == null)
            {
                LogWarningMissingPrimaryKey(entry);
                return null;
            }

            // Guard Clause 2: Composite Primary Key found
            if (primaryKey.Properties.Count != 1)
            {
                LogWarningCompositePrimaryKey(entry);
                return null;
            }

            // --- We know we have a single primary key property here ---
            var pkProperty = primaryKey.Properties.First();
            var pkPropertyEntry = entry.Property(pkProperty.Name);

            // Determine the value to check first (Current, unless state is Deleted)
            bool useOriginalFirst = entry.State == EntityState.Deleted;
            object? primaryValue = useOriginalFirst
                                     ? pkPropertyEntry?.OriginalValue
                                     : pkPropertyEntry?.CurrentValue;

            // Check if the primary value is a valid integer
            if (primaryValue is int intValue)
            {
                return intValue; // Found valid PK int value
            }

            // Log if the primary value exists but is not an integer
            if (primaryValue != null)
            {
                LogWarningPrimaryKeyNotInt(entry, pkProperty.Name, primaryValue);
            }

            // --- Fallback Check: Try the other value (Original if Current was tried, impossible if Original was tried first) ---
            // Only attempt fallback if the state is not Deleted (meaning we tried CurrentValue first)
            if (!useOriginalFirst) // Equivalent to entry.State != EntityState.Deleted
            {
                object? originalValue = pkPropertyEntry?.OriginalValue;
                if (originalValue is int originalIntValue)
                {
                    return originalIntValue; // Found valid PK int value in OriginalValue
                }
            }

            // If we reached here, no valid integer PK could be determined
            LogWarningReturningNullPrimaryKey(entry);
            return null;
        }

        // --- Private Logging Helper Methods ---

        private void LogWarningMissingPrimaryKey(EntityEntry entry)
        {
            _logger.LogWarning("Primary Key not found for entity type '{EntityType}' in table '{TableName}'.",
            entry.Entity.GetType().Name, entry.Metadata.GetTableName() ?? "<Unknown>");
        }

        private void LogWarningCompositePrimaryKey(EntityEntry entry)
        {
            _logger.LogWarning("Entity type '{EntityType}' in table '{TableName}' has a composite PK, not supported by simple GetPrimaryKeyValue.",
            entry.Entity.GetType().Name, entry.Metadata.GetTableName() ?? "<Unknown>");
        }

        private void LogWarningPrimaryKeyNotInt(EntityEntry entry, string pkName, object pkValue)
        {
            _logger.LogWarning("PK '{PKName}' in table '{TableName}' is not of type INT. Value: {PKValue}, Type: {PKType}",
            pkName, entry.Metadata.GetTableName() ?? "<Unknown>", pkValue, pkValue.GetType().Name);
        }

        private void LogWarningReturningNullPrimaryKey(EntityEntry entry)
        {
            _logger.LogWarning("Returning null PK for entity type '{EntityType}' in table '{TableName}' with state {EntityState}.",
            entry.Entity.GetType().Name, entry.Metadata.GetTableName() ?? "<Unknown>", entry.State);
        }


        /// <inheritdoc/>
        /// <remarks>
        /// This implementation looks for a property named "CropId" (case-insensitive) on the entity.
        /// It prioritizes the <c>CurrentValue</c>, falls back to <c>OriginalValue</c> for <see cref="EntityState.Deleted"/> or if CurrentValue is null.
        /// Returns the integer value if found and convertible, otherwise returns <c>null</c>.
        /// Does not currently attempt to load navigation properties.
        /// </remarks>
        public int? GetCropIdValue(EntityEntry entry)
        {
            // Finds a property named "CropId", ignoring case.
            var cropIdProp = entry.Properties.FirstOrDefault(p => p.Metadata.Name.Equals("CropId", StringComparison.OrdinalIgnoreCase));

            if (cropIdProp != null)
            {
                // Prioritizes CurrentValue, uses OriginalValue as fallback for Deleted state OR if Current is null.
                var value = (entry.State == EntityState.Deleted) ? cropIdProp.OriginalValue : (cropIdProp.CurrentValue ?? cropIdProp.OriginalValue);

                if (value is int cropId)
                {
                    return cropId;
                }
            }

            return null; // Returns null if CropId property not found or value is not an int.
        }

        /// <inheritdoc/>
        public IDictionary<string, object?>? GetValuesDictionary(PropertyValues? propertyValues)
        {
            return GetValuesDictionary(propertyValues, null); // Calls the overload
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Creates a dictionary of property names and their values from the provided <paramref name="propertyValues"/>.
        /// It excludes properties listed in the static <c>DefaultExcludedProperties</c> set (e.g., "Password")
        /// and any additional properties specified in the <paramref name="additionalExcludedProperties"/> argument.
        /// Returns <c>null</c> if the input <paramref name="propertyValues"/> is null or if the resulting dictionary is empty after exclusions.
        /// </remarks>
        public IDictionary<string, object?>? GetValuesDictionary(PropertyValues? propertyValues, IEnumerable<string>? additionalExcludedProperties)
        {
            if (propertyValues == null) return null;

            // Determine the final set of properties to exclude
            var finalExcludedProperties = DefaultExcludedProperties;
            if (additionalExcludedProperties?.Any() == true)
            {
                // Combine default and additional exclusions efficiently using HashSet
                finalExcludedProperties = new HashSet<string>(DefaultExcludedProperties.Concat(additionalExcludedProperties), StringComparer.OrdinalIgnoreCase);
            }

            var dictionary = new Dictionary<string, object?>();
            foreach (var property in propertyValues.Properties)
            {
                if (!finalExcludedProperties.Contains(property.Name))
                {
                    dictionary[property.Name] = propertyValues[property];
                }
            }

            return dictionary.Any() ? dictionary : null;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Serializes the provided dictionary of property values into a JSON string using pre-configured, shared serializer options
        /// (<see cref="_jsonSerializerOptions"/>). Handles null or empty input dictionaries by returning <c>null</c>.
        /// If serialization fails, it logs the error and returns a specific error placeholder string.
        /// </remarks>
        public string? SerializePropertyValueDictionary(IDictionary<string, object?>? values)
        {
            if (values == null || !values.Any())
            {
                return null;
            }

            try
            {
                return JsonSerializer.Serialize(values, _jsonSerializerOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error serializing property dictionary for auditing.");
                return $"<Error serializing values: {ex.Message}>";
            }
        }
    }
}