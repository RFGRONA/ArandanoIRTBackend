namespace ArandanoIRT_Backend.Infrastructure.Attributes 
{
    /// <summary>
    /// Attribute used to mark string properties on DTOs or models
    /// that should have HTML tags removed by the SanitizationActionFilter.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class SanitizeHtmlAttribute : Attribute
    {
        // No properties or methods needed for this simple marker attribute.
    }
}