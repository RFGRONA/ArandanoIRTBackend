using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace ArandanoIRT_Backend.Application.Interfaces.Auditing
{
    /// <summary>
    /// Defines a contract for a helper service providing common utility functions
    /// used during the audit logging process, particularly with Entity Framework Core entities.
    /// </summary>
    public interface IAuditHelperService
    {
        /// <summary>
        /// Gets the primary key value of the entity represented by the specified entry.
        /// Assumes the primary key is a single integer property.
        /// </summary>
        /// <param name="entry">The <see cref="EntityEntry"/> for the tracked entity.</param>
        /// <returns>The integer primary key value of the entity.</returns>
        int GetPrimaryKeyValue(EntityEntry entry);

        /// <summary>
        /// Gets the 'CropId' value (or equivalent identifier related to a crop)
        /// from the entity represented by the specified entry, if available.
        /// </summary>
        /// <param name="entry">The <see cref="EntityEntry"/> for the tracked entity.</param>
        /// <returns>The integer 'CropId' value, or <c>null</c> if the property doesn't exist, is not an integer, or its value is null.</returns>
        int? GetCropIdValue(EntityEntry entry);

        /// <summary>
        /// Creates a dictionary mapping property names to their values from the provided <see cref="PropertyValues"/> object,
        /// optionally excluding specified properties.
        /// </summary>
        /// <param name="propertyValues">The <see cref="PropertyValues"/> object (e.g., OriginalValues, CurrentValues)
        /// containing the entity's property values. Can be <c>null</c>.</param>
        /// <param name="excludedProperties">An optional collection of property names to exclude from the resulting dictionary.</param>
        /// <returns>
        /// A dictionary where keys are property names and values are the corresponding property values (as <c>object?</c>),
        /// or <c>null</c> if <paramref name="propertyValues"/> is <c>null</c>.
        /// </returns>
        IDictionary<string, object?>? GetValuesDictionary(PropertyValues? propertyValues, IEnumerable<string>? excludedProperties = null);

        /// <summary>
        /// Serializes the provided dictionary of property values into a string representation, typically JSON format.
        /// </summary>
        /// <param name="values">The dictionary containing property names and their values. Can be <c>null</c>.</param>
        /// <returns>
        /// A string representation of the dictionary (e.g., JSON formatted),
        /// or <c>null</c> if the input dictionary is <c>null</c> or empty.
        /// </returns>
        string? SerializePropertyValueDictionary(IDictionary<string, object?>? values);
    }
}