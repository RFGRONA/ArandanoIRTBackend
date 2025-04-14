namespace ArandanoIRT_Backend.Infrastructure.Services
{
    /// <summary>
    /// A background service responsible for periodically deleting old log files
    /// from the configured log directory to manage disk space.
    /// </summary>
    /// <remarks>
    /// Inherits from <see cref="BackgroundService"/> for long-running background task execution.
    /// </remarks>
    public class LogCleanupService : BackgroundService
    {
        /// <summary>
        /// The absolute path to the directory where log files are stored.
        /// </summary>
        private readonly string _logDirectory;
        /// <summary>
        /// Logger for recording service activity and errors.
        /// </summary>
        private readonly ILogger<LogCleanupService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="LogCleanupService"/> class.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        /// <param name="configuration">The application configuration (currently unused in this implementation but injected).</param>
        /// <exception cref="ArgumentNullException">Thrown if logger is null.</exception>
        public LogCleanupService(ILogger<LogCleanupService> logger, IConfiguration configuration) // configuration parameter kept as per original code
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            // Determines the log directory path based on the application's current directory.
            _logDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
            _logger.LogInformation("Log Cleanup Service initialized. Log directory: {LogDirectory}", _logDirectory);
        }

        /// <summary>
        /// Executes the background log cleanup task. Periodically calls the cleanup logic.
        /// This method is called when the <see cref="IHostedService"/> starts. The task returned should not complete until cancellation is requested.
        /// </summary>
        /// <param name="stoppingToken">A <see cref="CancellationToken"/> triggered when the application host requests shutdown.</param>
        /// <returns>A <see cref="Task"/> representing the long-running background service execution.</returns>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Log Cleanup Service started.");
            // Loops indefinitely until cancellation is requested.
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("Starting periodic log cleanup...");
                    // Performs the actual log cleanup logic.
                    CleanOldLogs(_logDirectory);
                    _logger.LogInformation("Log cleanup cycle completed successfully.");
                }
                catch (Exception ex) // Catches any errors during the cleanup process.
                {
                    _logger.LogError(ex, "Error occurred during log cleanup cycle.");
                }

                try
                {
                    // Waits for the specified delay (24 hours) before the next cleanup cycle,
                    // while respecting the cancellation token.
                    await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Expected exception when cancellation is requested during the delay.
                    _logger.LogInformation("Log Cleanup Service delay canceled.");
                    break; // Exit the loop if cancellation is requested during delay.
                }
            }
            _logger.LogInformation("Log Cleanup Service stopping.");
        }

        /// <summary>
        /// Finds and deletes log files in the specified directory that match the pattern "log-YYYYMMDD.txt"
        /// and are older than a defined threshold (currently 8 days based on creation time).
        /// </summary>
        /// <param name="logDirectory">The absolute path to the directory containing log files.</param>
        private void CleanOldLogs(string logDirectory)
        {
            // Check if the directory exists before attempting to get files.
            if (!Directory.Exists(logDirectory))
            {
                _logger.LogWarning("Log directory not found for cleanup: {LogDirectory}", logDirectory);
                return;
            }

            _logger.LogDebug("Scanning directory {LogDirectory} for old log files...", logDirectory);
            // Gets log files matching the specific pattern "log-" followed by 8 digits and ".txt".
            var files = Directory.GetFiles(logDirectory, "log-????????.txt");
            // Defines the age threshold for deletion (files older than 8 days).
            var deleteThreshold = DateTime.Now.AddDays(-8);
            int deletedCount = 0;

            // Iterates through the found log files.
            foreach (var file in files)
            {
                try
                {
                    // Gets the creation time of the file.
                    var creationDate = File.GetCreationTime(file);
                    // Checks if the file creation time is older than the threshold.
                    if (creationDate < deleteThreshold)
                    {
                        // Deletes the old log file.
                        File.Delete(file);
                        deletedCount++;
                        _logger.LogInformation("Deleted old log file: {FilePath}", file);
                    }
                }
                catch (IOException ioEx) // Catch specific IO errors (e.g., file in use)
                {
                    _logger.LogWarning(ioEx, "Could not delete log file (potentially in use): {FilePath}", file);
                }
                catch (UnauthorizedAccessException uaEx) // Catch permission errors
                {
                    _logger.LogError(uaEx, "Permission denied while trying to delete log file: {FilePath}", file);
                }
                catch (Exception ex) // Catch unexpected errors during file processing
                {
                    _logger.LogError(ex, "Error processing log file for deletion: {FilePath}", file);
                }
            }
            _logger.LogDebug("Finished scanning. Deleted {Count} old log files.", deletedCount);
        }
    }
}