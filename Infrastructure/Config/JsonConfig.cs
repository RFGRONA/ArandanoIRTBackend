using System.Text.Json.Serialization;
using System.Text.Json;

namespace ArandanoIRT_Backend.Infrastructure.Config
{
    /// <summary>
    /// Provides extension methods for configuring JSON serialization options
    /// for the application, specifically for controllers using System.Text.Json.
    /// </summary>
    public static class JsonConfig
    {
        /// <summary>
        /// Configures the default System.Text.Json serialization options used by ASP.NET Core controllers.
        /// Sets options for null value handling, property naming policy, and indentation.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> being configured. The method internally calls AddControllers().</param>
        /// <returns>The <see cref="IServiceCollection"/> so that additional configuration calls can be chained.</returns>
        public static IServiceCollection ConfigureJsonOptions(this IServiceCollection services)
        {
            // Configures JSON options for controllers added via AddControllers().
            services.AddControllers()
                .AddJsonOptions(options =>
                {
                    // Configures the serializer to ignore properties with null values when writing JSON output.
                    options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
                    // Sets the property naming policy to camelCase (e.g., propertyName becomes "propertyName" in JSON).
                    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                    // Disables indented formatting for the JSON output, resulting in more compact data.
                    options.JsonSerializerOptions.WriteIndented = false;
                });

            // Returns the IServiceCollection for chaining.
            return services;
        }
    }
}