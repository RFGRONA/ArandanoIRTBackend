using Serilog;

namespace ArandanoIRT_Backend.Infrastructure.Config
{
    public static class LoggingConfig
    {
        public static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            var oneUptimeConfig = configuration.GetSection("Logging:OneUptime");

            string? serviceName = oneUptimeConfig["ServiceName"];
            string? otlpEndpoint = oneUptimeConfig["OtlpEndpoint"];
            string? otlpToken = oneUptimeConfig["OtlpToken"];

            if (string.IsNullOrEmpty(serviceName) || string.IsNullOrEmpty(otlpEndpoint) || string.IsNullOrEmpty(otlpToken))
            {
                throw new ArgumentNullException("OneUptime logging configuration is missing required values.");
            }

            string logDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
            if (!Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }

            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console() 
                .WriteTo.File(
                    path: Path.Combine(logDirectory, "log-.txt"),
                    rollingInterval: RollingInterval.Day, 
                    retainedFileCountLimit: 7, 
                    restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Information
                )
                .WriteTo.OpenTelemetry(options =>
                {
                    options.Endpoint = otlpEndpoint;
                    options.Headers = new Dictionary<string, string>
                    {
                        { "x-oneuptime-token", otlpToken }
                    };
                    options.ResourceAttributes = new Dictionary<string, object>
                    {
                        { "service.name", serviceName }
                    };
                })
                .CreateLogger();

            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.ClearProviders();
                loggingBuilder.AddSerilog();
            });
        }
    }
}