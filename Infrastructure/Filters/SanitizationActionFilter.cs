using ArandanoIRT_Backend.Application.Interfaces.Utilities;
using ArandanoIRT_Backend.Infrastructure.Attributes;
using ArandanoIRT_Backend.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Reflection;

namespace ArandanoIRT_Backend.Infrastructure.Filters
{
    /// <summary>
    /// An ASP.NET Core action filter that automatically sanitizes string properties
    /// marked with the <see cref="SanitizeHtmlAttribute"/> on action method arguments (typically DTOs).
    /// Uses the registered <see cref="ISanitizerService"/> to remove potentially harmful elements.
    /// </summary>
    public class SanitizationActionFilter : IAsyncActionFilter
    {
        private readonly ISanitizerService _sanitizer;
        private readonly ILogger<SanitizationActionFilter> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="SanitizationActionFilter"/> class.
        /// </summary>
        /// <param name="sanitizer">The sanitizer service instance.</param>
        /// <param name="logger">The logger instance.</param>
        /// <exception cref="ArgumentNullException">Thrown if sanitizer or logger is null.</exception>
        public SanitizationActionFilter(ISanitizerService sanitizer, ILogger<SanitizationActionFilter> logger)
        {
            _sanitizer = sanitizer ?? throw new ArgumentNullException(nameof(sanitizer));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Called asynchronously before the action method executes.
        /// Inspects action arguments and sanitizes marked string properties.
        /// </summary>
        /// <param name="context">The context for the action executing.</param>
        /// <param name="next">The delegate representing the remaining action filter pipeline and the action method itself.</param>
        /// <returns>A <see cref="Task"/> that on completion indicates the filter has executed.</returns>
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            // Iterates through the arguments passed to the action method.
            foreach (var argument in context.ActionArguments.Values)
            {
                // Skips null arguments, value types, or direct strings (sanitization applies to properties of objects).
                if (argument == null || argument.GetType().IsValueType || argument is string)
                {
                    continue;
                }

                SanitizeObjectProperties(argument);
            }

            // Proceeds to execute the next filter or the action method itself.
            await next();
        }

        /// <summary>
        /// Sanitizes properties of a given object instance based on the SanitizeHtmlAttribute.
        /// </summary>
        /// <param name="obj">The object instance whose properties should be sanitized.</param>
        private void SanitizeObjectProperties(object obj)
        {
            // Gets the type of the object.
            var objectType = obj.GetType();
            // Gets all public instance properties of the object type.
            var properties = objectType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            // Iterates through the properties of the object.
            foreach (var property in properties)
            {
                SanitizePropertyIfNeeded(property, obj, objectType);
            }
        }

        /// <summary>
        /// Checks a specific property for the SanitizeHtmlAttribute and sanitizes its value if necessary.
        /// </summary>
        /// <param name="property">The PropertyInfo object representing the property.</param>
        /// <param name="targetObject">The object instance containing the property.</param>
        /// <param name="targetType">The type of the target object (passed for logging).</param>
        private void SanitizePropertyIfNeeded(PropertyInfo property, object targetObject, Type targetType)
        {
            // Checks if the property is marked with [SanitizeHtml].
            bool needsSanitization = property.GetCustomAttribute<SanitizeHtmlAttribute>() != null;
            if (!needsSanitization)
            {
                return; // Skip if attribute is not present
            }

            // Checks if the property is a writable string.
            bool isWritableString = property.PropertyType == typeof(string) && property.CanRead && property.CanWrite;

            if (isWritableString)
            {
                try
                {
                    // Gets the current value of the string property.
                    string? currentValue = property.GetValue(targetObject) as string;

                    // If the value is not null or empty, sanitize it.
                    if (!string.IsNullOrEmpty(currentValue))
                    {
                        string sanitizedValue = _sanitizer.Sanitize(currentValue);

                        // If sanitization changed the value, update the property on the argument object.
                        if (currentValue != sanitizedValue)
                        {
                            property.SetValue(targetObject, sanitizedValue);
                            _logger.LogDebug("Sanitized property {PropertyName} on type {TypeName}.", property.Name, targetType.Name);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Logs unexpected errors during reflection/sanitization but continues execution.
                    _logger.LogError(ex, "Error sanitizing property {PropertyName} on type {TypeName}.", property.Name, targetType.Name);
                }
            }
            else // needsSanitization was true, but it's not a writable string
            {
                // Logs a warning if [SanitizeHtml] is placed on an invalid property type.
                _logger.LogWarning("SanitizeHtmlAttribute placed on non-writable/non-string property {PropertyName} on type {TypeName}.", property.Name, targetType.Name);
            }
        }
    }
}