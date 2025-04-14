using Serilog;

namespace ArandanoIRT_Backend.Infrastructure.Config
{
    /// <summary>
    /// Provides extension methods for configuring application logging using Serilog.
    /// Sets up various sinks like Console, File, and OpenTelemetry based on configuration.
    /// </summary>
    public static class LoggingConfig
    {
        /// <summary>
        /// Configures Serilog as the logging provider for the application.
        /// Reads settings from configuration, sets up Console, rolling File, and OpenTelemetry sinks,
        /// and integrates Serilog with the standard .NET logging framework.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> to add logging services to.</param>
        /// <param name="configuration">The application <see cref="IConfiguration"/> instance, used for retrieving logging settings, especially for OpenTelemetry.</param>
        /// <exception cref="ArgumentNullException">Thrown if required configuration values under <c>Logging:OneUptime</c> (ServiceName, OtlpEndpoint, OtlpToken) are missing or empty.</exception>
        /// <remarks>
        /// This method configures the static <see cref="Log.Logger"/> instance and registers Serilog
        /// with the <see cref="IServiceCollection"/> using <c>AddLogging()</c>.
        /// </remarks>
        public static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            // Retrieves the specific configuration section for "OneUptime" (assumed OTLP target).
            var oneUptimeConfig = configuration.GetSection("Logging:OneUptime");

            // Reads required values for the OpenTelemetry sink.
            string? serviceName = oneUptimeConfig["ServiceName"];
            string? otlpEndpoint = oneUptimeConfig["OtlpEndpoint"];
            string? otlpToken = oneUptimeConfig["OtlpToken"];

            // Validates that required configuration values are present.
            if (string.IsNullOrEmpty(serviceName) || string.IsNullOrEmpty(otlpEndpoint) || string.IsNullOrEmpty(otlpToken))
            {
                // Throws if configuration is incomplete, preventing startup with misconfigured OTLP sink.
                // Consider logging a fatal error here as well if desired before throwing.
                throw new ArgumentNullException("OneUptime logging configuration is missing required values (ServiceName, OtlpEndpoint, OtlpToken).");
            }

            // Determines the directory for log files relative to the application's execution path.
            string logDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
            // Ensures the target log directory exists.
            if (!Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }

            // Configures the static Serilog logger instance.
            Log.Logger = new LoggerConfiguration()
                // Adds logging output to the console.
                .WriteTo.Console()
                // Adds logging output to a rolling file sink.
                .WriteTo.File(
                    path: Path.Combine(logDirectory, "log-.txt"), // Log file path pattern (date will be inserted).
                    rollingInterval: RollingInterval.Day, // Creates a new log file daily.
                    retainedFileCountLimit: 7, // Keeps log files for the last 7 days.
                    restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Information // Logs Information level and above to the file.
                )
                // Adds logging output to an OpenTelemetry Protocol (OTLP) endpoint.
                .WriteTo.OpenTelemetry(options =>
                {
                    options.Endpoint = otlpEndpoint; // Sets the OTLP receiver endpoint URL.
                    // Sets required headers, including the authentication token for OneUptime.
                    options.Headers = new Dictionary<string, string>
                    {
                        { "x-oneuptime-token", otlpToken }
                    };
                    // Sets resource attributes to identify the source service.
                    options.ResourceAttributes = new Dictionary<string, object>
                    {
                        { "service.name", serviceName }
                    };
                    // Protocol can be specified if needed, defaults often work (e.g., Grpc).
                    // options.Protocol = OtlpProtocol.Grpc;
                })
                // Creates the logger instance based on the configuration.
                .CreateLogger();

            // Integrates Serilog with the Microsoft.Extensions.Logging framework.
            services.AddLogging(loggingBuilder =>
            {
                // Clears any default logging providers (like ConsoleLoggerProvider).
                loggingBuilder.ClearProviders();
                // Adds Serilog as the sole logging provider.
                loggingBuilder.AddSerilog();
            });
        }
    }
}