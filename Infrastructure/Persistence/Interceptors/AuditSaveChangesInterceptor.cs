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
        // Injected services necessary for auditing.
        private readonly IRequestContextAccessor _requestContextAccessor;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IEnumerable<IAuditEntryGenerator> _auditGenerators; // Collection of all registered audit generators.
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
            IEnumerable<IAuditEntryGenerator> auditGenerators, // Injects the collection of generators.
            ILogger<AuditSaveChangesInterceptor> logger)
        {
            _requestContextAccessor = requestContextAccessor ?? throw new ArgumentNullException(nameof(requestContextAccessor));
            _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
            _auditGenerators = auditGenerators ?? throw new ArgumentNullException(nameof(auditGenerators)); // Stores the collection.
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
        /// The workflow is:
        /// 1. Get common audit metadata (user, IP, time) once.
        /// 2. Iterate through tracked entity entries.
        /// 3. Skip unchanged, detached, or audit entities.
        /// 4. Find the appropriate <see cref="IAuditEntryGenerator"/> for the entity type.
        /// 5. Generate audit entry/entries using the generator.
        /// 6. Collect all generated audit entries.
        /// 7. Add collected audit entries to the DbContext's change tracker.
        /// 8. Continue with the original SaveChanges operation.
        /// </remarks>
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            var context = eventData.Context;
            // Checks if the DbContext is available.
            if (context == null)
            {
                // Log message remains Spanish in code.
                _logger.LogWarning("DbContext es null en AuditSaveChangesInterceptor. No se puede auditar.");
                return await base.SavingChangesAsync(eventData, result, cancellationToken);
            }

            // List to accumulate all audit entries generated during this SaveChanges call.
            var allAuditEntries = new List<object>();

            // Get common audit metadata ONCE per SaveChanges call.
            // Uses try/catch in case the accessor fails (though unlikely if configured correctly).
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
                // Logs error obtaining context.
                _logger.LogError(ex, "Error getting context data for audit.");
                // Decides whether to continue without context data or throw; currently continues with nulls/unknown.
                ipAddress ??= "Unknown";
                userAgent ??= "Unknown";
            }

            // Gets the current timestamp.
            var performedAt = _dateTimeProvider.GetUtcNow();
            // Creates the metadata object.
            var metadata = new AuditMetadata(userId, ipAddress, userAgent, performedAt);

            // Logs the common metadata retrieved.
            _logger.LogInformation("Audit Interceptor: User={UserId}, IP={IP}, Agent={UserAgent}, Time={PerformedAt}",
                                   metadata.UserId ?? -1, metadata.IpAddress, metadata.UserAgent, metadata.PerformedAt);

            // Iterate over entities tracked by EF Core's ChangeTracker.
            foreach (var entry in context.ChangeTracker.Entries())
            {
                // Ignore detached, unchanged entities, or audit entities themselves to prevent infinite loops.
                if (entry.State == EntityState.Detached ||
                    entry.State == EntityState.Unchanged ||
                    IsAuditEntity(entry.Entity))
                {
                    continue;
                }

                // Logs processing attempt.
                _logger.LogInformation("Processing entity for auditing: {EntityName}, State: {State}",
                                       entry.Metadata.GetTableName() ?? entry.Entity.GetType().Name, entry.State);

                // Find the appropriate generator and generate entries.
                foreach (var generator in _auditGenerators)
                {
                    // Checks if the current generator can handle this entity type.
                    if (generator.CanHandle(entry))
                    {
                        _logger.LogDebug("Generator {GeneratorType} will handle entity {EntityType}",
                                         generator.GetType().Name, entry.Entity.GetType().Name);

                        // Calls the generator to create audit entries.
                        IEnumerable<object> generatedEntries = generator.GenerateEntries(entry, metadata);

                        // If entries were generated, add them to the master list.
                        if (generatedEntries != null && generatedEntries.Any())
                        {
                            allAuditEntries.AddRange(generatedEntries); // Adds the generated entries.
                            _logger.LogDebug("Generated {Count} audit entries via {GeneratorType}",
                                             generatedEntries.Count(), generator.GetType().Name);
                        }
                        // Assumes only one generator handles each entity type (breaks after first match).
                        break;
                    }
                }
            } // End of foreach (entry).

            // If any audit entries were generated, add them to the DbContext to be saved.
            if (allAuditEntries.Any())
            {
                _logger.LogInformation("Adding {AuditCount} total audit entries to the DbContext.", allAuditEntries.Count);
                // Adds the audit entries to the context's change tracker asynchronously.
                await context.AddRangeAsync(allAuditEntries, cancellationToken);
            }

            // Continues with the original SaveChanges operation (which will now include saving the audit entries).
            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        /// <summary>
        /// Helper method to determine if a given entity object is one of the known audit log entity types.
        /// Used to prevent auditing the audit log entries themselves.
        /// </summary>
        /// <param name="entity">The entity object to check.</param>
        /// <returns><c>true</c> if the entity is an audit log type; otherwise, <c>false</c>.</returns>
        private bool IsAuditEntity(object entity)
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