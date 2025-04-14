using ArandanoIRT_Backend.Domain.Exceptions;
using Newtonsoft.Json;
using System.Net;

namespace ArandanoIRT_Backend.UI.Middleware
{
    /// <summary>
    /// ASP.NET Core middleware for centralized exception handling.
    /// Catches unhandled exceptions during request processing and converts them
    /// into standardized JSON error responses with appropriate HTTP status codes.
    /// </summary>
    public class ExceptionHandlingMiddleware(RequestDelegate next) // Primary constructor
    {
        /// <summary>
        /// The next middleware delegate in the pipeline.
        /// </summary>
        private readonly RequestDelegate _next = next ?? throw new ArgumentNullException(nameof(next)); // Added null check

        /// <summary>
        /// Processes an HTTP request within the middleware pipeline.
        /// Executes the next middleware and handles any exceptions that occur.
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> for the current request.</param>
        /// <returns>A <see cref="Task"/> representing the processing of the request.</returns>
        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                // Calls the next middleware in the pipeline.
                await _next(context);
            }
            catch (Exception ex) // Catches any unhandled exceptions from downstream middleware or controllers.
            {
                // Logs the exception (consider injecting ILogger here if needed)
                // Log.Error(ex, "Unhandled exception caught by middleware for request {Path}", context.Request.Path);

                // Handles the exception and generates an appropriate response.
                await HandleExceptionAsync(context, ex);
            }
        }

        /// <summary>
        /// Handles the caught exception by setting the appropriate HTTP status code
        /// and writing a standardized JSON error response.
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> for the current request.</param>
        /// <param name="exception">The exception that was caught.</param>
        /// <returns>A <see cref="Task"/> representing the operation of writing the error response.</returns>
        private static Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            // Default status code for unhandled exceptions.
            var statusCode = HttpStatusCode.InternalServerError; // 500
            string? result; // Variable to hold the serialized JSON response body.

            // Determines the status code and response based on the type of exception caught.
            switch (exception)
            {
                case NotFoundException notFoundException:
                    statusCode = HttpStatusCode.NotFound; // 404 Not Found
                    result = JsonConvert.SerializeObject(new { error = notFoundException.Message });
                    break;

                case BusinessRuleViolationException businessException:
                    statusCode = HttpStatusCode.BadRequest; // 400 Bad Request
                    result = JsonConvert.SerializeObject(new { error = businessException.Message });
                    break;

                case DatabaseUpdateException dbException:
                    // Consider logging the full exception details for server-side errors.
                    statusCode = HttpStatusCode.InternalServerError; // 500 Internal Server Error
                    result = JsonConvert.SerializeObject(new { error = dbException.Message }); // Potentially expose internal details? Be cautious.
                    break;

                case ValidationException validationException:
                    statusCode = HttpStatusCode.BadRequest; // 400 Bad Request
                    result = JsonConvert.SerializeObject(new { error = validationException.Message });
                    break;

                default: // Handles any other unhandled exception types.
                    statusCode = HttpStatusCode.InternalServerError; // 500 Internal Server Error
                    // Generic error message translated to English.
                    result = JsonConvert.SerializeObject(new { error = "An unexpected server error occurred." });
                    // Consider logging the full 'exception' details here for unhandled errors.
                    break;
            }

            // Sets the response content type to JSON.
            context.Response.ContentType = "application/json";
            // Sets the HTTP status code determined by the exception type.
            context.Response.StatusCode = (int)statusCode;

            // Writes the serialized JSON result to the response body.
            return context.Response.WriteAsync(result ?? string.Empty); // Ensure result is not null
        }
    }

    /// <summary>
    /// Provides extension methods for easily registering the <see cref="ExceptionHandlingMiddleware"/>
    /// in the ASP.NET Core application pipeline.
    /// </summary>
    public static class ExceptionHandlingMiddlewareExtensions
    {
        /// <summary>
        /// Adds the <see cref="ExceptionHandlingMiddleware"/> to the specified <see cref="IApplicationBuilder"/> request pipeline.
        /// This should typically be registered early in the pipeline to catch exceptions from subsequent middleware and endpoints.
        /// </summary>
        /// <param name="builder">The <see cref="IApplicationBuilder"/> to add the middleware to.</param>
        /// <returns>The <see cref="IApplicationBuilder"/> so that additional calls can be chained.</returns>
        public static IApplicationBuilder UseExceptionHandling(this IApplicationBuilder builder)
        {
            // Registers the middleware component with the pipeline.
            return builder.UseMiddleware<ExceptionHandlingMiddleware>();
        }
    }
}