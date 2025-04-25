namespace ArandanoIRT_Backend.Infrastructure.Config
{
    /// <summary>
    /// Provides extension methods for configuring Cross-Origin Resource Sharing (CORS)
    /// policies for the application.
    /// </summary>
    public static class CORSConfig
    {
        private static readonly string[] _allAllowedOrigins = new[]
        {
            "http://localhost:3000", // Local development frontend
            "https://arandanoirt.co"  // Production frontend/domain
        };

        /// <summary>
        /// Configures and adds custom CORS policies to the application's service collection.
        /// Defines policies named "AllAllowed" and "OnlyFrontend" with specific origin configurations.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> to add CORS services to.</param>
        /// <returns>The <see cref="IServiceCollection"/> so that additional configuration calls can be chained.</returns>
        public static IServiceCollection AddCustomCors(this IServiceCollection services)
        {
            services.AddCors(options =>
            {
                // Defines the "AllAllowed" CORS policy.
                // Allows requests from specified development and production origins.
                options.AddPolicy("AllAllowed", builder =>
                {

                    builder.WithOrigins(_allAllowedOrigins) // Specifies the allowed origins.
                           .AllowAnyHeader()            // Allows any request header.
                           .AllowAnyMethod()            // Allows any HTTP method (GET, POST, PUT, etc.).
                           .AllowCredentials();         // Allows credentials (cookies, authorization headers) to be sent.
                });

                // Defines the "OnlyFrontend" CORS policy.
                // Allows requests specifically from the 'arandanoirt.co' origin.
                options.AddPolicy("OnlyFrontend", policy =>
                {
                    policy.WithOrigins("https://arandanoirt.co") // Specifies the single allowed origin.
                          .AllowAnyMethod()                   // Allows any HTTP method.
                          .AllowAnyHeader()                   // Allows any request header.
                          .AllowCredentials();                // Allows credentials.
                });

                // Additional policies can be added here if needed.
            });

            // Returns the IServiceCollection for chaining.
            return services;
        }
    }
}