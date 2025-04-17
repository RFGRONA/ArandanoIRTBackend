using ArandanoIRT_Backend.Domain.Exceptions;
using Newtonsoft.Json;
using System.Net;

namespace ArandanoIRT_Backend.UI.Middleware
{
    /// <summary>
    /// ASP.NET Core middleware for centralized exception handling.
    /// Catches unhandled exceptions, logs them, and converts them into standardized
    /// JSON error responses with appropriate HTTP status codes, avoiding leakage of internal details.
    /// </summary>
    public class ExceptionHandlingMiddleware 
    {
        /// <summary>
        /// The next middleware delegate in the pipeline.
        /// </summary>
        private readonly RequestDelegate _next;
        /// <summary>
        /// Logger for recording exceptions caught by the middleware.
        /// </summary>
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExceptionHandlingMiddleware"/> class.
        /// </summary>
        /// <param name="next">The next middleware delegate in the pipeline.</param>
        /// <param name="logger">The logger instance.</param>
        /// <exception cref="ArgumentNullException">Thrown if next or logger is null.</exception>
        public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Processes an HTTP request within the middleware pipeline.
        /// Executes the next middleware and handles any unhandled exceptions that occur by logging them
        /// and generating a standardized JSON error response.
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
            catch (Exception ex) // Catches any unhandled exceptions.
            {
                // Handles the exception to generate an appropriate response.
                await HandleExceptionAsync(context, ex);
            }
        }

        /// <summary>
        /// Handles the caught exception by logging the details, setting the appropriate HTTP status code,
        /// and writing a standardized JSON error response without leaking internal information.
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> for the current request.</param>
        /// <param name="exception">The exception that was caught.</param>
        /// <returns>A <see cref="Task"/> representing the operation of writing the error response.</returns>
        private Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            // Logs the full exception details for internal diagnostics.
            _logger.LogError(exception, "Unhandled exception caught by middleware for request path {Path}", context.Request.Path);

            // Default status code for unhandled exceptions.
            var statusCode = HttpStatusCode.InternalServerError; // 500
            string message; // User-facing error message.

            // Determines the status code and user-facing message based on the exception type.
            switch (exception)
            {
                case NotFoundException notFoundException:
                    statusCode = HttpStatusCode.NotFound; // 404 Not Found
                    // Assumes NotFoundException.Message is safe to display. Review if necessary.
                    message = notFoundException.Message;
                    break;

                case BusinessRuleViolationException businessException:
                    statusCode = HttpStatusCode.BadRequest; // 400 Bad Request
                                                            // Assumes BusinessRuleViolationException.Message is safe to display. Review if necessary.
                    message = businessException.Message;
                    break;

                case ValidationException validationException:
                    statusCode = HttpStatusCode.BadRequest; // 400 Bad Request
                    // Assumes ValidationException.Message is safe to display. Review if necessary.
                    message = validationException.Message;
                    break;

                // --- Defensive Handling for Internal/Database Errors ---
                case DatabaseUpdateException dbException:
                    statusCode = HttpStatusCode.InternalServerError; // 500 Internal Server Error
                    // Uses a generic message instead of dbException.Message to avoid leaking DB details.
                    message = "An error occurred while processing database changes.";
                    break;

                // --- Default Handling for any other Exception ---
                default:
                    statusCode = HttpStatusCode.InternalServerError; // 500 Internal Server Error
                                                                     // Uses a generic message for all other unexpected errors.
                    message = "An unexpected server error occurred.";
                    break;
            }

            // Serializes the user-facing message.
            var result = JsonConvert.SerializeObject(new { error = message });

            // Sets the response content type and status code.
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)statusCode;

            // Writes the serialized JSON result to the response body.
            return context.Response.WriteAsync(result);
        }
    }

    /// <summary>
    /// Provides an extension method for easily registering the <see cref="ExceptionHandlingMiddleware"/>
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