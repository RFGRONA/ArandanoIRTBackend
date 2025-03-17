using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

namespace ArandanoIRT_Backend.Infrastructure.Config
{
    public static class RateLimitConfig
    {
        public static void ConfigureRateLimiting(IServiceCollection services, IConfiguration configuration)
        {
            // Externalizamos los parámetros de configuración en appsettings.json o variables de entorno
            var rateLimitingConfig = configuration.GetSection("RateLimiting");
            var permitLimit = rateLimitingConfig.GetValue<int>("PermitLimit");
            var windowMinutes = rateLimitingConfig.GetValue<int>("WindowMinutes");
            var queueLimit = rateLimitingConfig.GetValue<int>("QueueLimit");


            services.AddRateLimiter(options =>
            {
                options.AddFixedWindowLimiter("fixed", limiterOptions =>
                {
                    limiterOptions.PermitLimit = permitLimit;
                    limiterOptions.Window = TimeSpan.FromMinutes(windowMinutes);
                    limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                    limiterOptions.QueueLimit = queueLimit;
                });

                options.OnRejected = async (context, cancellationToken) =>
                {
                    // Se obtiene el logger a través del proveedor de servicios para registrar la incidencia
                    var logger = context.HttpContext.RequestServices
                        .GetService<ILoggerFactory>()?
                        .CreateLogger("RateLimiting");
                    logger?.LogWarning("Se ha excedido el límite de solicitudes para la IP {IP}", context.HttpContext.Connection.RemoteIpAddress);

                    // En este ejemplo, en lugar de eliminar cookies, simplemente se devuelve una respuesta JSON
                    context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    context.HttpContext.Response.ContentType = "application/json";
                    var jsonResponse = "{\"error\":\"Demasiadas solicitudes\", \"message\":\"Intenta de nuevo más tarde.\"}";
                    await context.HttpContext.Response.WriteAsync(jsonResponse, cancellationToken);
                };
            });
        }
    }
}
