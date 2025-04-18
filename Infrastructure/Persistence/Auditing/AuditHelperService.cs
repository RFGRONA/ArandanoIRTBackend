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

        /// <summary>
        /// Defines a set of common sensitive property names (case-insensitive)
        /// to be excluded by default during the serialization of entity values for auditing.
        /// </summary>
        // Common sensitive properties to exclude from serialization.
        private static readonly HashSet<string> DefaultExcludedProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            nameof(Person.Password) // Excludes the password hash by default.
        };

        /// <summary>
        /// Shared, pre-configured JSON serializer options optimized for audit logging.
        /// Configuration includes: no indentation, ignoring null values, handling cycles, and converting enums to strings.
        /// </summary>
        // Shared serialization options.
        private static readonly JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions
        {
            WriteIndented = false, // Disables pretty-printing for compactness.
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, // Omits null properties.
            ReferenceHandler = ReferenceHandler.IgnoreCycles, // Prevents errors with circular references in object graphs.
            Converters = { new JsonStringEnumConverter() } // Serializes enums as strings instead of numbers.
            // Consider Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping; // If specific character encoding is needed (e.g., avoid escaping '+' etc.)
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
            // Finds the primary key definition for the entity.
            var primaryKey = entry.Metadata.FindPrimaryKey();

            // Handles single-property primary keys.
            if (primaryKey != null && primaryKey.Properties.Count == 1)
            {
                var pkProperty = primaryKey.Properties.First();
                // Prioritizes CurrentValue, but uses OriginalValue as fallback for Deleted state.
                var pkValue = entry.State == EntityState.Deleted
                                ? entry.Property(pkProperty.Name)?.OriginalValue
                                : entry.Property(pkProperty.Name)?.CurrentValue;

                // Checks if the retrieved value is an integer.
                if (pkValue is int intValue)
                {
                    return intValue; // Returns the valid integer PK.
                }
                else if (pkValue != null) // Logs a warning if PK value is not null but also not an integer.
                {
                    // Log message remains Spanish in code.
                    _logger.LogWarning("PK '{PKName}' en tabla '{TableName}' no es de tipo INT. Valor: {PKValue}, Tipo: {PKType}",
                                       pkProperty.Name, entry.Metadata.GetTableName() ?? "<Unknown>", pkValue, pkValue.GetType().Name);
                }

                // Tries OriginalValue if CurrentValue was null and state is not Deleted.
                if (entry.State != EntityState.Deleted)
                {
                    pkValue = entry.Property(pkProperty.Name)?.OriginalValue;
                    if (pkValue is int intValueOrig)
                    {
                        return intValueOrig; // Returns the valid integer PK from original value.
                    }
                }
                // If reached here after checking CurrentValue and OriginalValue, it's not a valid int PK.
            }
            // Logs warnings for missing or composite primary keys.
            else if (primaryKey == null)
            {
                // Log message remains Spanish in code.
                _logger.LogWarning("No se encontró Primary Key para la entidad tipo '{EntityType}' en tabla '{TableName}'.",
                                   entry.Entity.GetType().Name, entry.Metadata.GetTableName() ?? "<Unknown>");
            }
            else // Handles composite primary key case.
            {
                // Log message remains Spanish in code.
                _logger.LogWarning("La entidad tipo '{EntityType}' en tabla '{TableName}' tiene una PK compuesta, no soportado por GetPrimaryKeyValue simple.",
                                   entry.Entity.GetType().Name, entry.Metadata.GetTableName() ?? "<Unknown>");
            }

            // Returns null as the default value if an integer PK could not be retrieved.
            _logger.LogWarning("Returning null PK for entity type '{EntityType}' in table '{TableName}' with state {EntityState}.",
                               entry.Entity.GetType().Name, entry.Metadata.GetTableName() ?? "<Unknown>", entry.State);
            return null; 
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
                // Prioritizes CurrentValue, uses OriginalValue as fallback for Deleted state.
                var value = entry.State == EntityState.Deleted ? cropIdProp.OriginalValue : cropIdProp.CurrentValue;
                if (value is int cropId)
                {
                    return cropId;
                }
                // Tries OriginalValue if CurrentValue was null and state is not Deleted.
                if (entry.State != EntityState.Deleted)
                {
                    value = cropIdProp.OriginalValue;
                    if (value is int originalCropId) return originalCropId;
                }
            }

            // Could attempt searching navigation properties if strictly necessary,
            // but complex logic or lazy loading in interceptors is best avoided.
            // _logger.LogDebug("Propiedad CropId no encontrada directamente en {EntityType}.", entry.Entity.GetType().Name);
            return null; // Returns null if CropId property not found or value is not an int.
        }

        /// <inheritdoc/>
        // Implementation for the overload without additional excluded properties.
        public IDictionary<string, object?>? GetValuesDictionary(PropertyValues? propertyValues)
        {
            // This overload calls the more specific overload, passing null for the additional excluded properties.
            // This maintains the original behavior where only default exclusions were applied if none were specified by the caller.
            return GetValuesDictionary(propertyValues, null); // <<< Calls the other overload
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Creates a dictionary of property names and their values from the provided <paramref name="propertyValues"/>.
        /// It excludes properties listed in the static <c>DefaultExcludedProperties</c> set (e.g., "Password")
        /// and any additional properties specified in the <paramref name="additionalExcludedProperties"/> argument.
        /// Returns <c>null</c> if the input <paramref name="propertyValues"/> is null or if the resulting dictionary is empty after exclusions.
        /// </remarks>
        public IDictionary<string, object?>? GetValuesDictionary(PropertyValues? propertyValues, IEnumerable<string>? additionalExcludedProperties) // <<< Signature matches interface
        {
            if (propertyValues == null) return null;

            // Start with the default exclusions.
            var finalExcludedProperties = DefaultExcludedProperties;
            // If additional exclusions are provided and are not empty, combine them with the defaults.
            if (additionalExcludedProperties != null && additionalExcludedProperties.Any())
            {
                // Uses a HashSet for efficient O(1) lookups during exclusion checks.
                // Create a new HashSet to avoid modifying the static DefaultExcludedProperties set.
                finalExcludedProperties = new HashSet<string>(DefaultExcludedProperties.Concat(additionalExcludedProperties), StringComparer.OrdinalIgnoreCase);
            }

            var dictionary = new Dictionary<string, object?>();
            // Iterates through all properties in the PropertyValues collection.
            foreach (var property in propertyValues.Properties)
            {
                // If the property name is not in the combined exclusion list...
                if (!finalExcludedProperties.Contains(property.Name))
                {
                    // ...adds the property name and its value to the dictionary.
                    dictionary[property.Name] = propertyValues[property];
                }
            }

            // Returns the dictionary only if it contains any entries after exclusions, otherwise null.
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
            // Returns null if the input dictionary is null or empty.
            if (values == null || !values.Any())
            {
                return null;
            }

            try
            {
                // Serializes the dictionary using the statically defined options.
                return JsonSerializer.Serialize(values, _jsonSerializerOptions);
            }
            catch (Exception ex) // Catches potential serialization errors.
            {
                _logger.LogError(ex, "Error serializing property dictionary for auditing.");
                // Returns a placeholder string indicating the error.
                return $"<Error serializing values: {ex.Message}>"; // Error message in English for consistency
            }
        }
    }
}
