using ArandanoIRT_Backend.Application.Interfaces.Auditing; 
using ArandanoIRT_Backend.Application.Interfaces.Utilities; 
using ArandanoIRT_Backend.Infrastructure.Data; 
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ArandanoIRT_Backend.Infrastructure.Persistence.Interceptors
{
    /// <summary>
    /// An <see cref="SaveChangesInterceptor"/> that automatically generates audit log entries
    /// for entity changes detected by the DbContext's ChangeTracker before saving.
    /// It uses registered <see cref="IAuditEntryGenerator"/> instances to create specific audit records.
    /// </summary>
    public class AuditSaveChangesInterceptor : SaveChangesInterceptor
    {
        private readonly IRequestContextAccessor _requestContextAccessor;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IEnumerable<IAuditEntryGenerator> _auditGenerators;
        private readonly ILogger<AuditSaveChangesInterceptor> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="AuditSaveChangesInterceptor"/> class.
        /// </summary>
        /// <param name="requestContextAccessor">Accessor for retrieving current request context (user ID, IP, etc.).</param>
        /// <param name="dateTimeProvider">Provider for obtaining consistent UTC timestamps.</param>
        /// <param name="auditGenerators">An enumerable collection of registered audit entry generators.</param>
        /// <param name="logger">Logger for recording interceptor activity.</param>
        /// <exception cref="ArgumentNullException">Thrown if any injected service is null.</exception>
        public AuditSaveChangesInterceptor(
            IRequestContextAccessor requestContextAccessor,
            IDateTimeProvider dateTimeProvider,
            IEnumerable<IAuditEntryGenerator> auditGenerators,
            ILogger<AuditSaveChangesInterceptor> logger)
        {
            _requestContextAccessor = requestContextAccessor ?? throw new ArgumentNullException(nameof(requestContextAccessor));
            _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
            _auditGenerators = auditGenerators ?? throw new ArgumentNullException(nameof(auditGenerators));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Intercepts the saving changes operation asynchronously before it executes.
        /// Generates audit entries for detected changes and adds them to the DbContext to be saved
        /// within the same transaction.
        /// </summary>
        /// <param name="eventData">Contextual information about the SaveChanges operation.</param>
        /// <param name="result">The current interception result.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A <see cref="ValueTask{TResult}"/> representing the asynchronous operation, containing the possibly modified interception result.</returns>
        /// <remarks>
        /// Refactored workflow:
        /// 1. Get DbContext.
        /// 2. Get common audit metadata via helper.
        /// 3. Generate all audit entries for tracked changes via helper.
        /// 4. Add generated entries to the DbContext if any exist.
        /// 5. Continue with the original SaveChanges operation.
        /// </remarks>
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            var context = eventData.Context;
            if (context == null)
            {
                // Log message remains Spanish in code.
                _logger.LogWarning("DbContext is null in AuditSaveChangesInterceptor. Cannot audit.");
                // Use await before calling base async method
                return await base.SavingChangesAsync(eventData, result, cancellationToken);
            }

            // Get metadata (extracted logic)
            var metadata = GetAuditMetadata();

            // Generate entries (extracted logic)
            var allAuditEntries = GenerateAuditEntriesForChanges(context, metadata);

            // Add generated entries to the context if any were created
            if (allAuditEntries.Count != 0)
            {
                _logger.LogInformation("Adding {AuditCount} total audit entries to the DbContext.", allAuditEntries.Count);
                // Use await with AddRangeAsync
                await context.AddRangeAsync(allAuditEntries, cancellationToken);
            }

            // Continue with the original SaveChanges operation
            // Use await before calling base async method
            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        /// <summary>
        /// Retrieves common audit metadata (User ID, IP, User Agent, Timestamp).
        /// </summary>
        /// <returns>An <see cref="AuditMetadata"/> object.</returns>
        private AuditMetadata GetAuditMetadata()
        {
            int? userId = null;
            string? ipAddress = null;
            string? userAgent = null;
            try
            {
                userId = _requestContextAccessor.GetCurrentUserId();
                ipAddress = _requestContextAccessor.GetIpAddress();
                userAgent = _requestContextAccessor.GetUserAgent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting context data for audit.");
                // Continue with nulls/unknown for metadata, logging handled it.
                ipAddress ??= "Unknown"; // Assign Unknown if still null
                userAgent ??= "Unknown"; // Assign Unknown if still null
            }

            var performedAt = _dateTimeProvider.GetUtcNow();
            var metadata = new AuditMetadata(userId, ipAddress, userAgent, performedAt);

            _logger.LogInformation("Audit Interceptor Metadata: User={UserId}, IP={IP}, Agent={UserAgent}, Time={PerformedAt}",
                                   metadata.UserId ?? -1, metadata.IpAddress, metadata.UserAgent, metadata.PerformedAt);

            return metadata;
        }

        /// <summary>
        /// Generates a list of audit entry objects based on the changes tracked in the DbContext.
        /// </summary>
        /// <param name="context">The DbContext containing the tracked changes.</param>
        /// <param name="metadata">The common audit metadata.</param>
        /// <returns>A list of generated audit entry objects.</returns>
        private List<object> GenerateAuditEntriesForChanges(DbContext context, AuditMetadata metadata)
        {
            var allAuditEntries = new List<object>();

            // Iterate over tracked entries
            foreach (var entry in context.ChangeTracker.Entries())
            {
                // Filter out irrelevant entries
                if (entry.State == EntityState.Detached ||
                    entry.State == EntityState.Unchanged ||
                    IsAuditEntity(entry.Entity))
                {
                    continue;
                }

                _logger.LogInformation("Processing entity for auditing: {EntityName}, State: {State}",
                                       entry.Metadata.GetTableName() ?? entry.Entity.GetType().Name, entry.State);

                // Find the first generator that can handle this entity type
                foreach (var generator in _auditGenerators)
                {
                    if (generator.CanHandle(entry))
                    {
                        _logger.LogDebug("Generator {GeneratorType} will handle entity {EntityType}",
                                         generator.GetType().Name, entry.Entity.GetType().Name);

                        // Generate entries using the found generator
                        IEnumerable<object> generatedEntries = generator.GenerateEntries(entry, metadata);

                        // Add generated entries (if any) to the list
                        if (generatedEntries?.Any() == true)
                        {
                            allAuditEntries.AddRange(generatedEntries);
                            _logger.LogDebug("Generated {Count} audit entries via {GeneratorType}",
                                             generatedEntries.Count(), generator.GetType().Name);
                        }
                        // Assume only one generator per entity type, break inner loop
                        break;
                    }
                } // End foreach generator
            } // End foreach entry

            return allAuditEntries;
        }


        /// <summary>
        /// Helper method to determine if a given entity object is one of the known audit log entity types.
        /// Used to prevent auditing the audit log entries themselves.
        /// </summary>
        /// <param name="entity">The entity object to check.</param>
        /// <returns><c>true</c> if the entity is an audit log type; otherwise, <c>false</c>.</returns>
        private static bool IsAuditEntity(object entity)
        {
            // Checks if the entity belongs to one of the known audit entity types.
            return entity is Auditcrop ||
                   entity is Auditperson ||
                   entity is Auditdevice ||
                   entity is Auditdatatable ||
                   entity is Auditsensitivedata ||
                   entity is Auditsystemtable;
            // Add other audit entity types here if they exist.
        }
    }
}