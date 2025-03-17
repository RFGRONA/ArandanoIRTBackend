namespace ArandanoIRT_Backend.Infrastructure.Services
{
    public class LogCleanupService : BackgroundService
    {
        private readonly string _logDirectory;
        private readonly ILogger<LogCleanupService> _logger;

        public LogCleanupService(ILogger<LogCleanupService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _logDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    CleanOldLogs(_logDirectory);
                    _logger.LogInformation("Log cleanup completed successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during log cleanup");
                }
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
        }

        private void CleanOldLogs(string logDirectory)
        {
            var files = Directory.GetFiles(logDirectory, "log-????????.txt");
            foreach (var file in files)
            {
                var creationDate = File.GetCreationTime(file);
                if (creationDate < DateTime.Now.AddDays(-8))
                {
                    File.Delete(file);
                    _logger.LogInformation($"Deleted old log file: {file}");
                }
            }
        }
    }
}
