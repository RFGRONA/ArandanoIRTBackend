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
    /// Uses the registered <see cref="SanitizerService"/> to remove HTML tags.
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
                // Skips null arguments or value types (sanitization applies to properties of objects).
                if (argument == null || argument.GetType().IsValueType || argument is string)
                {
                    continue;
                }

                // Gets the type of the argument (e.g., the DTO type).
                var argumentType = argument.GetType();

                // Gets all public instance properties of the argument type.
                var properties = argumentType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

                // Iterates through the properties of the argument object.
                foreach (var property in properties)
                {
                    // Checks if the property is marked with [SanitizeHtml] and is a writable string.
                    bool needsSanitization = property.GetCustomAttribute<SanitizeHtmlAttribute>() != null;
                    bool isWritableString = property.PropertyType == typeof(string) && property.CanRead && property.CanWrite;

                    if (needsSanitization && isWritableString)
                    {
                        try
                        {
                            // Gets the current value of the string property.
                            string? currentValue = property.GetValue(argument) as string;

                            // If the value is not null or empty, sanitize it.
                            if (!string.IsNullOrEmpty(currentValue))
                            {
                                string sanitizedValue = _sanitizer.Sanitize(currentValue);

                                // If sanitization changed the value, update the property on the argument object.
                                if (currentValue != sanitizedValue)
                                {
                                    property.SetValue(argument, sanitizedValue);
                                    _logger.LogDebug("Sanitized property {PropertyName} on type {TypeName}.", property.Name, argumentType.Name);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            // Logs unexpected errors during reflection/sanitization but continues execution.
                            _logger.LogError(ex, "Error sanitizing property {PropertyName} on type {TypeName}.", property.Name, argumentType.Name);
                        }
                    }
                    else if (needsSanitization && !isWritableString)
                    {
                        // Logs a warning if [SanitizeHtml] is placed on an invalid property type.
                        _logger.LogWarning("SanitizeHtmlAttribute placed on non-writable string property {PropertyName} on type {TypeName}.", property.Name, argumentType.Name);
                    }
                }
            }

            // Proceeds to execute the next filter or the action method itself.
            await next();
        }
    }
}