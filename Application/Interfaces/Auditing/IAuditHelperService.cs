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
        /// Gets the primary key value of the entity represented by the specified entry,
        /// assuming the primary key is a single integer property.
        /// </summary>
        /// <param name="entry">The <see cref="EntityEntry"/> for the tracked entity.</param>
        /// <returns>
        /// The integer primary key value of the entity if found and is a single integer key,
        /// otherwise <c>null</c>.
        /// </returns>
        int? GetPrimaryKeyValue(EntityEntry entry); 

        /// <summary>
        /// Gets the 'CropId' value (or equivalent identifier related to a crop)
        /// from the entity represented by the specified entry, if available.
        /// </summary>
        /// <param name="entry">The <see cref="EntityEntry"/> for the tracked entity.</param>
        /// <returns>The integer 'CropId' value, or <c>null</c> if the property doesn't exist, is not an integer, or its value is null.</returns>
        int? GetCropIdValue(EntityEntry entry);

        /// <summary>
        /// Creates a dictionary mapping property names to their values from the provided <see cref="PropertyValues"/> object,
        /// excluding a predefined set of sensitive properties (like 'Password').
        /// </summary>
        /// <param name="propertyValues">The <see cref="PropertyValues"/> object (e.g., OriginalValues, CurrentValues)
        /// containing the entity's property values. Can be <c>null</c>.</param>
        /// <returns>
        /// A dictionary where keys are property names and values are the corresponding property values (as <c>object?</c>),
        /// excluding default sensitive properties. Returns <c>null</c> if <paramref name="propertyValues"/> is <c>null</c>
        /// or the resulting dictionary is empty after exclusions.
        /// </returns>
        IDictionary<string, object?>? GetValuesDictionary(PropertyValues? propertyValues); 

        /// <summary>
        /// Creates a dictionary mapping property names to their values from the provided <see cref="PropertyValues"/> object,
        /// excluding both a predefined set of sensitive properties and any additionally specified properties.
        /// </summary>
        /// <param name="propertyValues">The <see cref="PropertyValues"/> object (e.g., OriginalValues, CurrentValues)
        /// containing the entity's property values. Can be <c>null</c>.</param>
        /// <param name="additionalExcludedProperties">An optional collection of additional property names to exclude from the resulting dictionary,
        /// beyond the default exclusions. Can be <c>null</c> or empty.</param> // 
        /// <returns>
        /// A dictionary where keys are property names and values are the corresponding property values (as <c>object?</c>),
        /// excluding default and specified properties. Returns <c>null</c> if <paramref name="propertyValues"/> is <c>null</c>
        /// or the resulting dictionary is empty after exclusions.
        /// </returns>
        IDictionary<string, object?>? GetValuesDictionary(PropertyValues? propertyValues, IEnumerable<string>? additionalExcludedProperties);

        /// <summary>
        /// Serializes the provided dictionary of property values into a string representation, typically JSON format.
        /// </summary>
        /// <param name="values">The dictionary containing property names and their values. Can be <c>null</c>.</param>
        /// <returns>
        /// A string representation of the dictionary (e.g., JSON formatted),
        /// or <c>null</c> if the input dictionary is <c>null</c> or empty. Returns an error placeholder string on serialization failure.
        /// </returns>
        string? SerializePropertyValueDictionary(IDictionary<string, object?>? values); 
    }
 }