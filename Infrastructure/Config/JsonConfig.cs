using System.Text.Json.Serialization;
using System.Text.Json;

namespace ArandanoIRT_Backend.Infrastructure.Config
{
    public static class JsonConfig
    {
        public static IServiceCollection ConfigureJsonOptions(this IServiceCollection services)
        {
            services.AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
                    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                    options.JsonSerializerOptions.WriteIndented = false;
                });
            return services;
        }
    }
}
