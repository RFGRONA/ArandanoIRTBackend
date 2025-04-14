using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;

namespace ArandanoIRT_Backend.Infrastructure.Services
{
    /// <summary>
    /// An Swagger operation filter that adds standard HTTP error response types (400, 404, 429, 500)
    /// to every operation in the OpenAPI documentation.
    /// </summary>
    public class ProducesResponseTypeFilter : IOperationFilter
    {
        /// <summary>
        /// Applies the filter to the specified OpenAPI operation.
        /// </summary>
        /// <param name="operation">The OpenAPI operation to modify.</param>
        /// <param name="context">The context providing information about the operation.</param>
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            // Ensures common error responses are documented for all operations.
            operation.Responses.TryAdd("400", new OpenApiResponse { Description = "Bad Request - The request was invalid or cannot be otherwise served." });
            operation.Responses.TryAdd("404", new OpenApiResponse { Description = "Not Found - The requested resource could not be found." });
            operation.Responses.TryAdd("429", new OpenApiResponse { Description = "Too Many Requests - The client has sent too many requests in a given amount of time." });
            operation.Responses.TryAdd("500", new OpenApiResponse { Description = "Internal Server Error - An unexpected condition was encountered." });
        }
    }

    /// <summary>
    /// An Swagger operation filter that adds the 401 Unauthorized response type
    /// to operations that are protected by an Authorize attribute (identified by name).
    /// </summary>
    /// <remarks>
    /// This filter identifies the Authorize attribute by comparing the attribute's type name as a string.
    /// </remarks>
    public class UnauthorizedResponseFilter : IOperationFilter
    {
        /// <summary>
        /// Applies the filter to the specified OpenAPI operation.
        /// </summary>
        /// <param name="operation">The OpenAPI operation to modify.</param>
        /// <param name="context">The context providing information about the operation and method attributes.</param>
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            // Checks if the action method or its declaring controller class has an attribute named "AuthorizeAttribute".
            var hasAuthorize = context.MethodInfo.DeclaringType?.GetCustomAttributes(true)
                                  .Union(context.MethodInfo.GetCustomAttributes(true))
                                  .Any(attr => attr.GetType().Name == "AuthorizeAttribute") ?? false;

            // If an Authorize attribute is found, add the 401 response if not already present.
            if (hasAuthorize)
            {
                operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Unauthorized - Authentication is required and has failed or has not yet been provided." });
            }
        }
    }

    /// <summary>
    /// An Swagger operation filter that attempts to determine the actual return type for 200 OK responses
    /// (handling Task and IActionResult with ProducesResponseType) and sets the corresponding
    /// JSON schema in the OpenAPI documentation.
    /// </summary>
    public class ProducesJsonFilter : IOperationFilter
    {
        /// <summary>
        /// Applies the filter to the specified OpenAPI operation.
        /// </summary>
        /// <param name="operation">The OpenAPI operation to modify.</param>
        /// <param name="context">The context providing information about the operation, method, and schema generation.</param>
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            // Gets the declared return type of the controller action method.
            var returnType = context.MethodInfo.ReturnType;
            Type? actualReturnType = null;

            // Checks if there's an explicit [ProducesResponseType(200, Type = ...)] attribute.
            var produceResponseTypeAttribute = context.MethodInfo
                .GetCustomAttributes(true)
                .OfType<ProducesResponseTypeAttribute>()
                .FirstOrDefault(a => a.StatusCode == StatusCodes.Status200OK);

            if (produceResponseTypeAttribute != null && produceResponseTypeAttribute.Type != null)
            {
                // Uses the type specified in the attribute.
                actualReturnType = produceResponseTypeAttribute.Type;
            }
            else if (returnType != null) // If no explicit attribute, inspect the method's return type.
            {
                // Handles Task<T> return types.
                if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
                {
                    // Gets the T from Task<T>.
                    var taskType = returnType.GetGenericArguments()[0];
                    // If T is IActionResult or derived, looks for [ProducesResponseType(200)] on the method again.
                    if (typeof(IActionResult).IsAssignableFrom(taskType))
                    {
                        var methodAttributes = context.MethodInfo.GetCustomAttributes<ProducesResponseTypeAttribute>();
                        var okResponseType = methodAttributes.FirstOrDefault(a => a.StatusCode == StatusCodes.Status200OK);
                        if (okResponseType != null && okResponseType.Type != typeof(void)) // Ensure type is specified
                        {
                            actualReturnType = okResponseType.Type;
                        }
                        // If IActionResult without explicit ProducesResponseType(200, Type), cannot determine schema reliably.
                    }
                    else
                    {
                        // Assumes T is the actual return type.
                        actualReturnType = taskType;
                    }
                }
                // Handles direct IActionResult return types.
                else if (typeof(IActionResult).IsAssignableFrom(returnType))
                {
                    // Looks for [ProducesResponseType(200, Type = ...)] to determine the actual type.
                    var methodAttributes = context.MethodInfo.GetCustomAttributes<ProducesResponseTypeAttribute>();
                    var okResponseType = methodAttributes.FirstOrDefault(a => a.StatusCode == StatusCodes.Status200OK);
                    if (okResponseType != null && okResponseType.Type != typeof(void)) // Ensure type is specified
                    {
                        actualReturnType = okResponseType.Type;
                    }
                    // If IActionResult without explicit ProducesResponseType(200, Type), cannot determine schema reliably.
                }
                // Handles direct return types (not Task or IActionResult).
                else if (returnType != typeof(void) && returnType != typeof(Task)) // Exclude void/Task
                {
                    actualReturnType = returnType;
                }
            }

            // If an actual return type for a 200 OK response was determined...
            if (actualReturnType != null)
            {
                // Ensures a 200 response entry exists.
                if (!operation.Responses.ContainsKey("200"))
                {
                    operation.Responses.Add("200", new OpenApiResponse { Description = "Success" }); // Add basic description
                }

                var response = operation.Responses["200"];

                // Generates the OpenAPI schema for the determined return type.
                var schema = context.SchemaGenerator.GenerateSchema(actualReturnType, context.SchemaRepository);

                // Sets the content type to application/json and assigns the generated schema.
                response.Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new OpenApiMediaType
                    {
                        Schema = schema
                    }
                };
            }
        }
    }

    /// <summary>
    /// An Swagger operation filter that adds a security requirement definition (referencing "cookieAuth")
    /// to operations protected by the <see cref="AuthorizeAttribute"/>.
    /// </summary>
    public class AuthOperationFilter : IOperationFilter
    {
        /// <summary>
        /// Applies the filter to the specified OpenAPI operation.
        /// </summary>
        /// <param name="operation">The OpenAPI operation to modify.</param>
        /// <param name="context">The context providing information about the operation and method attributes.</param>
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            // Checks if the action method or its declaring controller class has the AuthorizeAttribute applied.
            var hasAuthorize = context.MethodInfo.DeclaringType?.GetCustomAttributes(true)
                                   .OfType<AuthorizeAttribute>().Any() ?? false ||
                               context.MethodInfo.GetCustomAttributes(true)
                                   .OfType<AuthorizeAttribute>().Any();

            // If the operation requires authorization...
            if (hasAuthorize)
            {
                // Adds the security requirement section to the operation definition.
                // This tells Swagger UI to include options for the defined "cookieAuth" security scheme.
                operation.Security =
                [ // Uses collection expression C# 12 syntax
                    new OpenApiSecurityRequirement() {
                        {
                            new OpenApiSecurityScheme
                            {
                                Reference = new OpenApiReference
                                {
                                    Type = ReferenceType.SecurityScheme,
                                    Id = "cookieAuth" // Must match the ID defined in AddSecurityDefinition
                                }
                            },
                            Array.Empty<string>() // No specific scopes required for this scheme type
                        }
                    }
                ];
            }
        }
    }
}
