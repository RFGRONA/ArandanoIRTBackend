using ArandanoIRT_Backend.Infrastructure.Services;

namespace ArandanoIRT_Backend.UI.Middleware
{
    /// <summary>
    /// ASP.NET Core middleware that sanitizes incoming HTTP request query string parameters
    /// and all request header values by removing HTML tags using the <see cref="SanitizerService"/>.
    /// </summary>
    /// <remarks>
    /// This middleware directly modifies the <see cref="HttpRequest.QueryString"/> and
    /// <see cref="HttpRequest.Headers"/> collections. Modifying headers might have unintended consequences
    /// for certain standard headers (e.g., Authorization, Content-Type). Use with caution.
    /// </remarks>
    public class SanitizationMiddleware(RequestDelegate next, SanitizerService sanitizer) 
    {
        /// <summary>
        /// The next middleware delegate in the pipeline.
        /// </summary>
        private readonly RequestDelegate _next = next ?? throw new ArgumentNullException(nameof(next)); 
        /// <summary>
        /// The service used to perform HTML sanitization (tag removal).
        /// </summary>
        private readonly SanitizerService _sanitizer = sanitizer ?? throw new ArgumentNullException(nameof(sanitizer)); 

        /// <summary>
        /// Processes an HTTP request within the middleware pipeline.
        /// Sanitizes query parameters and headers before calling the next middleware.
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> for the current request.</param>
        /// <returns>A <see cref="Task"/> representing the processing of the request.</returns>
        public async Task InvokeAsync(HttpContext context)
        {
            // --- Sanitize Query String Parameters ---
            // Creates a new dictionary by sanitizing the value of each query parameter.
            var queryCollection = context.Request.Query.ToDictionary(
                kvp => kvp.Key,
                // Ensures null safety and sanitizes the value.
                kvp => _sanitizer.Sanitize(kvp.Value.ToString() ?? string.Empty)
            // Materializes the sanitized key-value pairs back into a dictionary suitable for QueryString.Create.
            ).ToDictionary(kvp => kvp.Key, kvp => (string?)kvp.Value); // Cast value to string? for Create method

            // Reconstructs the QueryString using the sanitized key-value pairs.
            // Note: This modifies the Request object directly.
            context.Request.QueryString = new QueryString(QueryString.Create(queryCollection).ToString());

            // --- Sanitize Request Headers ---
            // Iterates through all keys in the request header collection.
            foreach (var headerKey in context.Request.Headers.Keys)
            {
                // Gets the current header value(s).
                var headerValue = context.Request.Headers[headerKey];
                // Sanitizes the header value (converted to string).
                var sanitizedValue = _sanitizer.Sanitize(headerValue.ToString() ?? string.Empty);
                // Overwrites the header value in the request header collection with the sanitized version.
                // Warning: Modifying headers like Authorization, Content-Type, Accept might break functionality.
                context.Request.Headers[headerKey] = sanitizedValue;
            }

            // Calls the next middleware in the pipeline with the modified request context.
            await _next(context);
        }
    }

    /// <summary>
    /// Provides an extension method for easily registering the <see cref="SanitizationMiddleware"/>
    /// in the ASP.NET Core application pipeline.
    /// </summary>
    public static class SanitizationMiddlewareExtensions
    {
        /// <summary>
        /// Adds the <see cref="SanitizationMiddleware"/> to the specified <see cref="IApplicationBuilder"/> request pipeline.
        /// This middleware should be registered early in the pipeline, before components that might be vulnerable to XSS from query/headers.
        /// </summary>
        /// <param name="builder">The <see cref="IApplicationBuilder"/> to add the middleware to.</param>
        /// <returns>The <see cref="IApplicationBuilder"/> so that additional calls can be chained.</returns>
        public static IApplicationBuilder UseSanitization(this IApplicationBuilder builder)
        {
            // Registers the middleware component with the pipeline.
            return builder.UseMiddleware<SanitizationMiddleware>();
        }
    }
}