using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Interfaces;
using Microsoft.OpenApi.Models;
using System.Reflection;
using ArandanoIRT_Backend.Infrastructure.Services;

namespace ArandanoIRT_Backend.Infrastructure.Config
{
    /// <summary>
    /// Provides extension methods for configuring Swashbuckle Swagger/OpenAPI generation services.
    /// </summary>
    public static class SwaggerConfig
    {
        /// <summary>
        /// Configures Swashbuckle services for generating OpenAPI documentation.
        /// Sets up document information, security definitions for cookie authentication,
        /// custom operation filters, and integration with XML documentation comments.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> to add Swagger services to.</param>
        /// <remarks>
        /// This configuration includes the API description, contact info, terms of service,
        /// defines a "cookieAuth" security scheme, applies several custom operation filters
        /// (<see cref="ProducesResponseTypeFilter"/>, <see cref="UnauthorizedResponseFilter"/>,
        /// <see cref="ProducesJsonFilter"/>, <see cref="AuthOperationFilter"/>), and enables
        /// the use of XML comments from the assembly's documentation file.
        /// </remarks>
        public static void ConfigureSwagger(this IServiceCollection services)
        {
            // Adds Swagger generation services to the DI container.
            services.AddSwaggerGen(options =>
            {
                // Configures the main OpenAPI document information.
                options.SwaggerDoc("ArandanoIRT", new OpenApiInfo
                {
                    Title = "ArandanoIRT API",
                    // Description translated to English.
                    Description = "Data management and validation. For more details, see the [full documentation on GitHub](https://github.com/RFGRONA/ArandanoIRT/).",
                    // Sets contact information for the API support team.
                    Contact = new OpenApiContact
                    {
                        Name = "ArandanoIRT Support Team", // Translated
                        Email = "contacto@arandanoirt.co",
                        Url = new Uri("https://arandanoirt.co/help")
                    },
                    // Sets the URL for the terms of service.
                    TermsOfService = new Uri("https://arandanoirt.co/privacy-policy"),
                    // Adds custom extension properties to the OpenAPI document (e.g., link to wiki).
                    Extensions = new Dictionary<string, IOpenApiExtension>
                    {
                        { "x-documentation-url", new OpenApiString("https://github.com/RFGRONA/ArandanoIRT/wiki/") }
                    }
                });

                // Defines the 'cookieAuth' security scheme used for cookie-based authentication.
                options.AddSecurityDefinition("cookieAuth", new OpenApiSecurityScheme
                {
                    Name = "Cookie", // Specifies the location name (informative).
                    Type = SecuritySchemeType.ApiKey, // Using ApiKey type to represent cookie transport.
                    In = ParameterLocation.Cookie, // Indicates the key (cookie name) is found in the cookie header.
                    Description = "Authentication using cookies. User must log in and send requests with a valid authentication cookie." // Translated
                });

                // Registers custom operation filters to modify the generated OpenAPI operations.
                options.OperationFilter<ProducesResponseTypeFilter>(); // Adds standard error responses.
                options.OperationFilter<UnauthorizedResponseFilter>(); // Adds 401 response for authorized actions.
                options.OperationFilter<ProducesJsonFilter>(); // Sets JSON schema for 200 OK responses.
                options.OperationFilter<AuthOperationFilter>(); // Adds security requirements for authorized actions.

                // Get the Assembly where SwaggerConfig is defined.
                var currentAssembly = typeof(SwaggerConfig).Assembly;
                var assemblyName = currentAssembly.GetName().Name;

                if (!string.IsNullOrEmpty(assemblyName))
                {
                    var xmlFile = $"{assemblyName}.xml";
                    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);

                    // Check if the file exists before including to avoid warnings/errors.
                    if (File.Exists(xmlPath))
                    {
                        // Includes the XML comments found at the specified path.
                        options.IncludeXmlComments(xmlPath);
                    }
                }
            });
        }
    }
}
