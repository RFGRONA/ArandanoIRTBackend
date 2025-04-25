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
        // Using OfType<AuthorizeAttribute>() is generally safer than checking by name.
        var hasAuthorize = context.MethodInfo.DeclaringType?.GetCustomAttributes(true)
                                       .Union(context.MethodInfo.GetCustomAttributes(true))
                                       .OfType<AuthorizeAttribute>() // Use OfType for type safety
                                       .Any() ?? false;

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
        // Determine the actual return type using the helper method.
        var actualReturnType = DetermineActualReturnType(context);

        // If an actual return type for a 200 OK response was determined...
        if (actualReturnType != null)
        {
            // Ensure a 200 response entry exists.
            // Use TryAdd which is safer than checking ContainsKey then Add.
            if (!operation.Responses.TryGetValue("200", out OpenApiResponse? response))
            {
                response = new OpenApiResponse { Description = "Success" };
                operation.Responses.Add("200", response);
            }

            // Ensure description isn't overwritten if already present
            response.Description ??= "Success";

            // Generate the OpenAPI schema for the determined return type.
            var schema = context.SchemaGenerator.GenerateSchema(actualReturnType, context.SchemaRepository);

            // Set the content type to application/json and assign the generated schema.
            // Ensure Content dictionary exists before assigning to it.
            response.Content ??= new Dictionary<string, OpenApiMediaType>();
            response.Content["application/json"] = new OpenApiMediaType { Schema = schema };
        }
    }

    /// <summary>
    /// Determines the actual CLR type returned by an action method for a 200 OK response.
    /// Handles Task, IActionResult (with ProducesResponseType attribute), and direct types.
    /// </summary>
    /// <param name="context">The operation filter context.</param>
    /// <returns>The actual return Type, or null if it cannot be determined reliably.</returns>
    private static Type? DetermineActualReturnType(OperationFilterContext context)
    {
        var returnType = context.MethodInfo.ReturnType;

        // 1. Check for explicit [ProducesResponseType(200, Type = ...)] attribute first.
        var producesResponseTypeAttribute = context.MethodInfo
            .GetCustomAttributes(true)
            .OfType<ProducesResponseTypeAttribute>()
            .FirstOrDefault(a => a.StatusCode == StatusCodes.Status200OK);

        if (producesResponseTypeAttribute?.Type != null && producesResponseTypeAttribute.Type != typeof(void))
        {
            return producesResponseTypeAttribute.Type;
        }

        // 2. If no explicit attribute, inspect the method's declared return type.
        if (returnType == null || returnType == typeof(void) || returnType == typeof(Task))
        {
            // Void, Task -> no specific type information for 200 response content.
            return null;
        }

        // 3. Handle Task<T>
        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            var taskResultType = returnType.GetGenericArguments()[0];

            // If Task<IActionResult> or Task<ActionResult<T>>, we still need ProducesResponseType
            if (typeof(IActionResult).IsAssignableFrom(taskResultType))
            {
                // Reuse the attribute check from step 1 (already done above).
                // If producesResponseTypeAttribute was found with a valid Type, it would have returned already.
                // If it wasn't found or Type was void, we can't determine the type.
                return null;
            }
            else
            {
                // Assume T in Task<T> is the actual return type if it's not an IActionResult.
                return taskResultType;
            }
        }

        // 4. Handle IActionResult (non-Task)
        if (typeof(IActionResult).IsAssignableFrom(returnType))
        {
            // Reuse the attribute check from step 1.
            // If producesResponseTypeAttribute was found with a valid Type, it would have returned already.
            // If not, we can't determine the type.
            return null;
        }

        // 5. Handle direct return types (that are not void/Task/IActionResult)
        return returnType;
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
        // Check for Authorize attribute on method or controller using OfType<T> for safety.
        var hasAuthorize = context.MethodInfo.GetCustomAttributes(true).OfType<AuthorizeAttribute>().Any() ||
                           context.MethodInfo.DeclaringType?.GetCustomAttributes(true).OfType<AuthorizeAttribute>().Any() == true;

            if (hasAuthorize)
            {
                // Ensure Security Requirements list exists
                operation.Security ??= new List<OpenApiSecurityRequirement>();

                // Add the security requirement for cookieAuth
                operation.Security.Add(new OpenApiSecurityRequirement
                 {
                     {
                         new OpenApiSecurityScheme
                         {
                             Reference = new OpenApiReference
                             {
                                 Type = ReferenceType.SecurityScheme,
                                 Id = "cookieAuth" // Must match AddSecurityDefinition ID
                             }
                         },
                         Array.Empty<string>() // No specific scopes needed
                     }
                 });
            }
        }
    }
}
