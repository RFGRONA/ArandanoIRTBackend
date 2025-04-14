using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

namespace ArandanoIRT_Backend.Infrastructure.Config
{
    /// <summary>
    /// Provides extension methods for configuring request rate limiting policies
    /// for the application using Microsoft.AspNetCore.RateLimiting.
    /// </summary>
    public static class RateLimitConfig
    {
        /// <summary>
        /// Configures the application's request rate limiting services.
        /// Defines a fixed window rate limiter policy named "fixed" based on configuration settings
        /// and sets up a custom handler for rejected requests.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> to add rate limiting services to.</param>
        /// <param name="configuration">The application <see cref="IConfiguration"/> instance, used for retrieving rate limiting settings from the "RateLimiting" section.</param>
        /// <remarks>
        /// The configured policy name is "fixed". Rejected requests trigger logging and return a 429 status code with a JSON body.
        /// </remarks>
        public static void ConfigureRateLimiting(IServiceCollection services, IConfiguration configuration)
        {
            // Reads rate limiting parameters from configuration (e.g., appsettings.json or environment variables).
            var rateLimitingConfig = configuration.GetSection("RateLimiting");
            var permitLimit = rateLimitingConfig.GetValue<int>("PermitLimit"); // Max requests allowed in window.
            var windowMinutes = rateLimitingConfig.GetValue<int>("WindowMinutes"); // Duration of the sliding window.
            var queueLimit = rateLimitingConfig.GetValue<int>("QueueLimit"); // Max queued requests when limit is reached.

            // Adds the rate limiter services to the DI container.
            services.AddRateLimiter(options =>
            {
                // Defines a named policy "fixed" using a FixedWindowRateLimiter.
                options.AddFixedWindowLimiter("fixed", limiterOptions =>
                {
                    // Sets the maximum number of permits (requests) allowed within a window.
                    limiterOptions.PermitLimit = permitLimit;
                    // Sets the duration of the time window.
                    limiterOptions.Window = TimeSpan.FromMinutes(windowMinutes);
                    // Sets the order in which queued requests are processed (oldest first).
                    limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                    // Sets the maximum number of requests that can be queued when the limit is hit.
                    limiterOptions.QueueLimit = queueLimit;
                });

                // Defines a custom action to execute when a request is rejected by any limiter policy.
                options.OnRejected = async (context, cancellationToken) =>
                {
                    // Obtains a logger instance via the service provider to log the rejection event.
                    var logger = context.HttpContext.RequestServices
                        .GetService<ILoggerFactory>()?
                        .CreateLogger("RateLimiting"); // Creates logger with category "RateLimiting".

                    // Logs a warning indicating the rate limit was exceeded for the specific IP.
                    logger?.LogWarning("Rate limit exceeded for IP {IP}", context.HttpContext.Connection.RemoteIpAddress);

                    // Sets the HTTP response status code to 429 Too Many Requests.
                    context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    // Sets the response content type to JSON.
                    context.HttpContext.Response.ContentType = "application/json";
                    // Defines the JSON response body for rejected requests (in Spanish as per original code's intent).
                    var jsonResponse = "{\"error\":\"Demasiadas solicitudes\", \"message\":\"Intenta de nuevo más tarde.\"}";
                    // Writes the JSON response asynchronously.
                    await context.HttpContext.Response.WriteAsync(jsonResponse, cancellationToken);
                };
            });
        }
    }
}
