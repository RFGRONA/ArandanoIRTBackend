using ArandanoIRT_Backend.Domain.Entities;
using ArandanoIRT_Backend.Domain.IRepositories;
using ArandanoIRT_Backend.Domain.ValueObjects;
using ArandanoIRT_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Serilog;

using ArandanoIRT_Backend.Infrastructure.Interfaces.IServices;
using ArandanoIRT_Backend.Application.Interfaces.Utilities;

namespace ArandanoIRT_Backend.Infrastructure.Repositories
{
    /// <summary>
    /// Implements the <see cref="IStatusRepository"/> interface, providing data access logic
    /// for status entities (<see cref="StatusEntity"/>) using Entity Framework Core.
    /// Includes caching mechanisms to improve performance for frequently accessed status data.
    /// </summary>
    public class StatusRepository(ApplicationDbContext context, ICacheService cacheService, IDateTimeProvider dateTimeProvider, ILogger<StatusRepository> logger) : IStatusRepository // Added IDateTimeProvider based on field usage
    {
        /// <summary>
        /// The database context used for data access.
        /// </summary>
        private readonly ApplicationDbContext _context = context ?? throw new ArgumentNullException(nameof(context));
        /// <summary>
        /// The caching service used for storing and retrieving status data.
        /// </summary>
        private readonly ICacheService _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        /// <summary>
        /// Provider for obtaining consistent UTC timestamps (dependency added based on field usage).
        /// </summary>
        private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider)); 
        /// <summary>
        /// Logger instance for logging repository operations.
        /// </summary>
        private readonly ILogger<StatusRepository> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        /// <summary>
        /// Cache key for storing the list of all statuses.
        /// </summary>
        private const string CACHE_KEY_ALL_STATUSES = "Statuses_All";
        /// <summary>
        /// Format string for cache keys storing statuses related to a specific table name.
        /// </summary>
        private const string CACHE_KEY_TABLE_FORMAT = "Statuses_Table_{0}"; // {0} is lowercase table name
        /// <summary>
        /// Default duration for caching status data.
        /// </summary>
        private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1); // Cache for 1 hour

        /// <summary>
        /// Maps a database context <see cref="Status"/> entity to a domain <see cref="StatusEntity"/>.
        /// Also maps the nested <see cref="Data.Tablerelation"/> if included.
        /// </summary>
        /// <param name="status">The database entity instance.</param>
        /// <returns>The mapped domain entity instance, or <c>null</c> if input is null (throws NullReferenceException due to null! usage).</returns>
        /// <remarks>Uses reflection to set the <c>TableRelation</c> navigation property on the domain entity.</remarks>
        private static StatusEntity MapToDomainEntity(Status status)
        {
            // Guard against null input.
            if (status == null) return null!; // Consider throwing ArgumentNullException if null is truly unexpected.

            // Create the main domain entity.
            var statusEntity = new StatusEntity(
                status.Idstatus,
                status.Namestatus,
                status.Tablerelationid
            );

            // If the related TableRelation data was loaded from the DB, map it too.
            if (status.Tablerelation != null)
            {
                var tableRelationEntity = new TableRelationEntity(
                    status.Tablerelation.Idtablerelation,
                    status.Tablerelation.Tablename
                );
                // Use reflection to set the navigation property on the domain entity.
                var tableRelationProperty = typeof(StatusEntity).GetProperty(nameof(StatusEntity.TableRelation));
                if (tableRelationProperty?.CanWrite ?? false)
                {
                    tableRelationProperty.SetValue(statusEntity, tableRelationEntity, null);
                }
                else
                {
                    // Log if property couldn't be set (should not happen with current definition).
                    Log.Warning("Could not set TableRelation property via reflection on StatusEntity during mapping.");
                }
            }

            return statusEntity;
        }

        /// <summary>
        /// Maps a domain <see cref="StatusEntity"/> to a database context <see cref="Status"/> entity.
        /// Updates existing instance if provided, otherwise creates a new one.
        /// Does not map navigation properties like TableRelation.
        /// </summary>
        /// <param name="entity">The domain entity instance.</param>
        /// <param name="existingStatus">Optional. The existing database entity to update.</param>
        /// <returns>The mapped or updated database entity instance.</returns>
        private static Status MapToDbModel(StatusEntity entity, Status? existingStatus = null)
        {
            // Use existing instance or create a new one.
            var status = existingStatus ?? new Status();
            // Map properties from domain entity to database model.
            status.Idstatus = entity.IdStatus; // Usually ID is not set manually unless creating.
            status.Namestatus = entity.NameStatus;
            status.Tablerelationid = entity.TableRelationId;
            // Navigation property 'Tablerelation' is not mapped back.
            return status;
        }

        /// <inheritdoc/>
        public async Task<Result<IEnumerable<StatusEntity>>> GetStatusesByTableNameAsync(string tableName)
        {
            // Basic input validation.
            if (string.IsNullOrWhiteSpace(tableName))
                return Result<IEnumerable<StatusEntity>>.Failure("Table name cannot be empty.");

            // Generate cache key specific to the table name (lowercase).
            string cacheKey = string.Format(CACHE_KEY_TABLE_FORMAT, tableName.ToLowerInvariant());

            // Attempt to retrieve the statuses from the cache.
            var cachedStatuses = _cacheService.Get<IEnumerable<StatusEntity>>(cacheKey);
            if (cachedStatuses != null)
            {
                _logger.LogDebug("Cache hit for statuses by table name: {TableName}", tableName);
                return Result<IEnumerable<StatusEntity>>.Success(cachedStatuses);
            }

            // Cache miss: Proceed to query the database.
            _logger.LogDebug("Cache miss for statuses by table name: {TableName}. Querying database.", tableName);
            try
            {
                // Query statuses, including the related TableRelation, filtering by table name (case-insensitive).
                var statuses = await _context.Status
                                             .Include(s => s.Tablerelation) // Eager load relation to get table name
                                             .Where(s => s.Tablerelation != null &&
                                                         s.Tablerelation.Tablename.ToLower() == tableName.ToLower())
                                             .AsNoTracking() // Read-only query.
                                             .ToListAsync();

                // Map the database entities to domain entities.
                var domainStatuses = statuses.Select(MapToDomainEntity).ToList();

                // Log if no statuses were found for the given table name.
                if (domainStatuses.Count == 0)
                {
                    _logger.LogWarning("No statuses found in DB for table name: {TableName}", tableName);
                }
                else // If statuses were found, cache them.
                {
                    _cacheService.Set(cacheKey, domainStatuses, CacheDuration); // Set cache with duration.
                    _logger.LogInformation("Cached {Count} statuses for table name: {TableName}", domainStatuses.Count, tableName);
                }

                // Return the retrieved (and potentially cached) statuses.
                return Result<IEnumerable<StatusEntity>>.Success(domainStatuses);
            }
            catch (Exception ex) // Handle potential database errors.
            {
                _logger.LogError($"Error retrieving statuses for table {tableName}: {ex.Message}");
                return Result<IEnumerable<StatusEntity>>.Failure("Error retrieving statuses.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<StatusEntity>> GetById(int id)
        {
            // Attempt to get the status from the 'GetAll' cache first for efficiency.
            var allStatusesResult = await GetAll(); // This checks cache internally.

            if (allStatusesResult.IsSuccess && allStatusesResult.Value != null)
            {
                // Search within the cached list.
                var statusFromCache = allStatusesResult.Value.FirstOrDefault(s => s.IdStatus == id);
                if (statusFromCache != null)
                {
                    _logger.LogDebug("Cache hit for status ID {StatusId} via GetAll cache.", id);
                    return Result<StatusEntity>.Success(statusFromCache);
                }
            }

            // Cache miss (either GetAll failed or status not in the cached list). Query DB directly.
            _logger.LogDebug("Status ID {StatusId} not found in GetAll cache. Querying database directly.", id);
            try
            {
                // Query by ID, including TableRelation, no tracking.
                var status = await _context.Status
                                           .Include(s => s.Tablerelation)
                                           .AsNoTracking()
                                           .FirstOrDefaultAsync(s => s.Idstatus == id);

                // Return failure if not found in DB.
                if (status == null)
                    return Result<StatusEntity>.Failure("Status not found.");

                // Map and return success. (Do not cache single item retrieved this way).
                return Result<StatusEntity>.Success(MapToDomainEntity(status));
            }
            catch (Exception ex) // Handle potential database errors.
            {
                _logger.LogError( $"Error retrieving status by ID {id}: {ex.Message}");
                return Result<StatusEntity>.Failure("Error retrieving status by ID.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<IEnumerable<StatusEntity>>> GetAll()
        {
            // Attempt to retrieve all statuses from the cache first.
            var cachedStatuses = _cacheService.Get<IEnumerable<StatusEntity>>(CACHE_KEY_ALL_STATUSES);
            if (cachedStatuses != null)
            {
                _logger.LogDebug("Cache hit for GetAll statuses.");
                return Result<IEnumerable<StatusEntity>>.Success(cachedStatuses);
            }

            // Cache miss: Proceed to query the database.
            _logger.LogDebug("Cache miss for GetAll statuses. Querying database.");
            try
            {
                // Query all statuses, including TableRelation, no tracking.
                var statuses = await _context.Status
                                             .Include(s => s.Tablerelation)
                                             .AsNoTracking()
                                             .ToListAsync();

                // Map the results to domain entities.
                var domainStatuses = statuses.Select(MapToDomainEntity).ToList();

                // Store the retrieved list in the cache.
                _cacheService.Set(CACHE_KEY_ALL_STATUSES, domainStatuses, CacheDuration);
                _logger.LogInformation("Cached {Count} total statuses.", domainStatuses.Count);

                // Return the retrieved and cached statuses.
                return Result<IEnumerable<StatusEntity>>.Success(domainStatuses);
            }
            catch (Exception ex) // Handle potential database errors.
            {
                _logger.LogError($"Error retrieving all statuses: {ex.Message}");
                return Result<IEnumerable<StatusEntity>>.Failure("Error retrieving all statuses.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<StatusEntity>> Create(StatusEntity entity)
        {
            if (entity == null)
                return Result<StatusEntity>.Failure("Status entity cannot be null.");

            try
            {
                var statusDbModel = MapToDbModel(entity);
                await _context.Status.AddAsync(statusDbModel);
                int success = await _context.SaveChangesAsync();

                if (success == 0)
                {
                    _logger.LogWarning("Failed to save status to the database. Entity: {@StatusEntity}", entity);
                    return Result<StatusEntity>.Failure("Failed to save status to the database.");
                }

                // Invalidate only the general cache after creation, as getting table name reliably might require extra query.
                InvalidateStatusCache(); // Call simplified version without table name

                // --- Workaround: Update domain entity ID post-save ---
                var idProperty = typeof(StatusEntity).GetProperty(nameof(StatusEntity.IdStatus));
                if (idProperty?.CanWrite ?? false)
                {
                    idProperty.SetValue(entity, statusDbModel.Idstatus, null);
                }
                else
                {
                    _logger.LogWarning("Could not set IdStatus on domain entity after creation.");
                }
                // --- End Workaround ---

                return Result<StatusEntity>.Success(entity);
            }
            catch (DbUpdateException dbEx) // Handle DB errors.
            {
                _logger.LogError($"Database error creating status. Entity: {@entity}, {dbEx.InnerException?.Message ?? dbEx.Message}");
                return Result<StatusEntity>.Failure("Database error creating status.");
            }
            catch (Exception ex) // Handle general errors.
            {
                _logger.LogError($"Database error creating status. Entity: {@entity}, {ex.InnerException?.Message ?? ex.Message}");
                return Result<StatusEntity>.Failure("Error creating status.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<bool>> Update(StatusEntity entity)
        {
            if (entity == null)
                return Result<bool>.Failure("Status entity cannot be null.");

            try
            {
                // Find existing entity, including TableRelation to get original table info.
                var existingStatus = await _context.Status
                                                   .Include(s => s.Tablerelation) // Include relation
                                                   .FirstOrDefaultAsync(s => s.Idstatus == entity.IdStatus);

                if (existingStatus == null)
                    return Result<bool>.Failure($"Status with ID {entity.IdStatus} not found for update.");

                // Store original table name *before* mapping overwrites navigation property potentially.
                string? originalTableName = existingStatus.Tablerelation?.Tablename;

                // Map domain entity onto existing tracked entity.
                MapToDbModel(entity, existingStatus);
                // Explicitly set UpdatedAt if needed (consider if your entity/mapper handles this)
                // existingStatus.Updatedat = _dateTimeProvider.GetUtcNow(); // Example if needed

                // Marks for update and saves.
                _context.Status.Update(existingStatus);
                int rowsAffected = await _context.SaveChangesAsync();

                // If update was successful, invalidate cache using the original table name.
                if (rowsAffected > 0)
                {
                    // Invalidate based on the *original* table name.
                    InvalidateStatusCache(originalTableName);
                }
                else
                {
                    _logger.LogWarning("No changes were detected or saved for status ID: {StatusId}", entity.IdStatus);
                }

                return Result<bool>.Success(rowsAffected > 0);
            }
            catch (DbUpdateConcurrencyException ex) // Handle concurrency conflicts.
            {
                _logger.LogWarning(ex, "Concurrency conflict updating status with ID {StatusId}", entity.IdStatus);
                return Result<bool>.Failure("Concurrency conflict updating status.");
            }
            catch (DbUpdateException dbEx) // Handle other DB update errors.
            {
                _logger.LogError($"Database error updating status ID: {entity.IdStatus}. Entity: {@entity}. Message: {dbEx.InnerException?.Message ?? dbEx.Message}");
                return Result<bool>.Failure("Database error updating status.");
            }
            catch (Exception ex) // Handle general errors.
            {
                _logger.LogError($"Error updating status ID: {entity.IdStatus}. Entity: {entity}, {ex.Message}");
                return Result<bool>.Failure("Error updating status.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<bool>> Delete(int id)
        {
            try
            {
                // Find entity, including TableRelation for cache invalidation info.
                var status = await _context.Status
                                           .Include(s => s.Tablerelation) // Include relation
                                           .FirstOrDefaultAsync(s => s.Idstatus == id);

                // Returns failure if not found.
                if (status == null)
                    return Result<bool>.Failure($"Status with ID {id} not found for deletion.");

                // Store table info *before* removing.
                string? tableName = status.Tablerelation?.Tablename;

                // Removes from context and saves.
                _context.Status.Remove(status);
                int rowsAffected = await _context.SaveChangesAsync();

                // If successful, invalidate cache using the table name.
                if (rowsAffected > 0)
                {
                    InvalidateStatusCache(tableName);
                }
                else
                {
                    _logger.LogWarning("Failed to delete status (no rows affected) ID: {StatusId}", id);
                }

                return Result<bool>.Success(rowsAffected > 0);
            }
            catch (DbUpdateException dbEx) // Handle DB errors (e.g., FK constraints).
            {
                _logger.LogError($"Database error deleting status ID: {id} (check for related records): {dbEx.InnerException?.Message ?? dbEx.Message}");
                return Result<bool>.Failure($"Database error deleting status (check for related records).");
            }
            catch (Exception ex) // Handle general errors.
            {
                _logger.LogError($"Error deleting status ID: {id}, {ex.Message}");
                return Result<bool>.Failure("Error deleting status.");
            }
        }

        /// <summary>
        /// Invalidates cache entries related to statuses.
        /// Always removes the "All Statuses" cache entry.
        /// Also removes the table-specific cache entry if the table name is provided.
        /// </summary>
        /// <param name="tableName">Optional. The specific table name whose status cache should be invalidated.</param>
        private void InvalidateStatusCache(string? tableName = null) // Simplified: only takes optional tableName
        {
            try
            {
                // Always invalidate the cache for all statuses.
                _cacheService.Remove(CACHE_KEY_ALL_STATUSES);
                _logger.LogDebug("Invalidated cache key: {CacheKey}", CACHE_KEY_ALL_STATUSES);

                // If a specific table name is provided, invalidate its cache too.
                if (!string.IsNullOrWhiteSpace(tableName))
                {
                    string tableCacheKey = string.Format(CACHE_KEY_TABLE_FORMAT, tableName.ToLowerInvariant());
                    _cacheService.Remove(tableCacheKey);
                    _logger.LogDebug("Invalidated cache key for table {TableName}: {CacheKey}", tableName, tableCacheKey);
                }
                else
                {
                    // Fallback: Invalidate all table-specific cache entries if tableName is not provided.
                    var allTableNames = _context.Tablerelation.Select(tr => tr.Tablename.ToLowerInvariant()).Distinct().ToList();
                    foreach (var table in allTableNames)
                    {
                        string tableCacheKey = string.Format(CACHE_KEY_TABLE_FORMAT, table);
                        _cacheService.Remove(tableCacheKey);
                        _logger.LogDebug("Invalidated cache key for table {TableName}: {CacheKey}", table, tableCacheKey);
                    }
                }
                // Removed the database lookup logic based on tableRelationId.
            }
            catch (Exception ex)
            {
                // Log errors during cache invalidation but do not let them fail the main operation.
                _logger.LogError(ex, "Error during status cache invalidation for TableName {TableName}.", tableName ?? "<null>");
            }
        }
    }
}