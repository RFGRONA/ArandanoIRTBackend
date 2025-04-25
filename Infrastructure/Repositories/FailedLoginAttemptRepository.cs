using ArandanoIRT_Backend.Application.Interfaces.Utilities;
using ArandanoIRT_Backend.Domain.Entities;
using ArandanoIRT_Backend.Domain.IRepositories;
using ArandanoIRT_Backend.Domain.ValueObjects;
using ArandanoIRT_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ArandanoIRT_Backend.Infrastructure.Repositories
{
    /// <summary>
    /// Implements the <see cref="IFailedLoginAttemptRepository"/> interface, providing data access logic
    /// for failed login attempt entities (<see cref="FailedLoginAttemptEntity"/>) using Entity Framework Core.
    /// </summary>
    public class FailedLoginAttemptRepository(ApplicationDbContext context, IDateTimeProvider dateTimeProvider, ILogger<FailedLoginAttemptRepository> logger) : IFailedLoginAttemptRepository
    {
        /// <summary>
        /// The database context used for data access.
        /// </summary>
        private readonly ApplicationDbContext _context = context ?? throw new ArgumentNullException(nameof(context));
        /// <summary>
        /// Provider for obtaining consistent UTC timestamps.
        /// </summary>
        private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
        /// <summary>
        /// Logger instance for logging repository operations and errors.
        /// </summary>
        private readonly ILogger<FailedLoginAttemptRepository> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        /// <summary>
        /// Maps a database context <see cref="Failedloginattempt"/> entity to a domain <see cref="FailedLoginAttemptEntity"/>.
        /// </summary>
        /// <param name="attempt">The database entity instance.</param>
        /// <returns>The mapped domain entity instance, or <c>null</c> if input is null (throws NullReferenceException due to null! usage).</returns>
        /// <remarks>
        /// Handles potential null values for string and PersonId properties from the database by mapping them to defaults (empty string or 0).
        /// </remarks>
        private static FailedLoginAttemptEntity MapToDomainEntity(Failedloginattempt attempt)
        {
            // Guards against null input, though null forgiving operator suggests it shouldn't be null.
            if (attempt == null) return null!;
            return new FailedLoginAttemptEntity(
                attempt.Idfailedloginattempt,
                attempt.Attemptdate,
                attempt.Ipaddress ?? string.Empty, // Maps potential DB null to empty string.
                attempt.Deviceinfo ?? string.Empty, // Maps potential DB null to empty string.
                attempt.Useragent ?? string.Empty, // Maps potential DB null to empty string.
                attempt.Personid ?? 0 // Maps potential DB null PersonId to 0.
            );
        }

        /// <summary>
        /// Maps a domain <see cref="FailedLoginAttemptEntity"/> to a database context <see cref="Failedloginattempt"/> entity.
        /// Updates existing instance if provided, otherwise creates a new one.
        /// </summary>
        /// <param name="entity">The domain entity instance.</param>
        /// <param name="existingAttempt">Optional. The existing database entity to update.</param>
        /// <returns>The mapped or updated database entity instance.</returns>
        private static Failedloginattempt MapToDbModel(FailedLoginAttemptEntity entity, Failedloginattempt? existingAttempt = null)
        {
            // Uses existing instance or creates a new one.
            var attempt = existingAttempt ?? new Failedloginattempt();
            // Maps properties from domain entity to database model.
            attempt.Idfailedloginattempt = entity.IdFailedLoginAttempt; // Usually ID is not set manually unless updating.
            attempt.Attemptdate = entity.AttemptDate;
            attempt.Ipaddress = entity.IpAddress;
            attempt.Deviceinfo = entity.DeviceInfo;
            attempt.Useragent = entity.UserAgent;
            attempt.Personid = entity.PersonId; // Maps domain int to DB nullable int?.
            return attempt;
        }

        /// <inheritdoc/>
        public async Task<Result<IEnumerable<FailedLoginAttemptEntity>>> GetRecentAttemptsAsync(int personId, DateTime since)
        {
            try
            {
                // Ensures the comparison uses the provided 'since' timestamp.
                var sinceDate = since;
                // Queries for attempts for the person since the specified time, ordered descending. No tracking needed.
                var attempts = await _context.Failedloginattempt
                                             .AsNoTracking()
                                             .Where(a => a.Personid == personId && a.Attemptdate >= sinceDate)
                                             .OrderByDescending(a => a.Attemptdate)
                                             .ToListAsync();
                // Maps the results and returns success.
                return Result<IEnumerable<FailedLoginAttemptEntity>>.Success(attempts.Select(MapToDomainEntity));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving recent failed attempts for person ID {PersonId}", personId);
                return Result<IEnumerable<FailedLoginAttemptEntity>>.Failure("Error retrieving recent failed attempts for person.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<IEnumerable<FailedLoginAttemptEntity>>> GetRecentAttemptsByIpAsync(string ipAddress, DateTime since)
        {
            // Basic input validation.
            if (string.IsNullOrWhiteSpace(ipAddress))
                return Result<IEnumerable<FailedLoginAttemptEntity>>.Failure("IP address cannot be empty.");
            try
            {
                // Ensures the comparison uses the provided 'since' timestamp.
                var sinceDate = since;
                // Queries for attempts from the IP since the specified time, ordered descending. No tracking needed.
                var attempts = await _context.Failedloginattempt
                                             .AsNoTracking()
                                             .Where(a => a.Ipaddress == ipAddress && a.Attemptdate >= sinceDate)
                                             .OrderByDescending(a => a.Attemptdate)
                                             .ToListAsync();
                // Maps the results and returns success.
                return Result<IEnumerable<FailedLoginAttemptEntity>>.Success(attempts.Select(MapToDomainEntity));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving recent failed attempts for IP {IpAddress}", ipAddress);
                return Result<IEnumerable<FailedLoginAttemptEntity>>.Failure("Error retrieving recent failed attempts.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<FailedLoginAttemptEntity>> GetById(int id)
        {
            try
            {
                // Retrieves a single attempt by its PK, no tracking.
                var attempt = await _context.Failedloginattempt.AsNoTracking().FirstOrDefaultAsync(a => a.Idfailedloginattempt == id);
                // Returns failure if not found.
                if (attempt == null) return Result<FailedLoginAttemptEntity>.Failure("Failed attempt record not found.");
                // Maps and returns success.
                return Result<FailedLoginAttemptEntity>.Success(MapToDomainEntity(attempt));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving failed attempt by ID {FailedAttemptId}", id);
                return Result<FailedLoginAttemptEntity>.Failure("Error retrieving failed attempt by ID.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<IEnumerable<FailedLoginAttemptEntity>>> GetAll()
        {
            try
            {
                // Retrieves all attempts, no tracking.
                var attempts = await _context.Failedloginattempt.AsNoTracking().ToListAsync();
                // Maps the list and returns success.
                return Result<IEnumerable<FailedLoginAttemptEntity>>.Success(attempts.Select(MapToDomainEntity));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all failed attempts.");
                return Result<IEnumerable<FailedLoginAttemptEntity>>.Failure("Error retrieving all failed attempts.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<FailedLoginAttemptEntity>> Create(FailedLoginAttemptEntity entity)
        {
            // Basic null check.
            if (entity == null) return Result<FailedLoginAttemptEntity>.Failure("Failed attempt entity cannot be null.");
            try
            {
                // Maps to DB model.
                var dbModel = MapToDbModel(entity);
                // Sets AttemptDate if not already set.
                if (dbModel.Attemptdate == default) dbModel.Attemptdate = _dateTimeProvider.GetUtcNow();

                // Adds to context and saves.
                await _context.Failedloginattempt.AddAsync(dbModel);
                await _context.SaveChangesAsync();

                // --- Workaround: Update domain entity ID post-save ---
                var idProperty = typeof(FailedLoginAttemptEntity).GetProperty(nameof(FailedLoginAttemptEntity.IdFailedLoginAttempt));
                if (idProperty?.CanWrite == true)
                {
                    idProperty.SetValue(entity, dbModel.Idfailedloginattempt, null);
                }
                else
                {
                    _logger.LogWarning("Could not set IdFailedLoginAttempt on domain entity after creation.");
                }

                // Workaround: Update AttemptDate post-save
                var dateProperty = typeof(FailedLoginAttemptEntity).GetProperty(nameof(FailedLoginAttemptEntity.AttemptDate));
                if (dateProperty?.CanWrite == true)
                {
                    dateProperty.SetValue(entity, dbModel.Attemptdate, null);
                }
                else
                {
                    _logger.LogWarning("Could not set AttemptDate on domain entity after creation.");
                }
                // --- End Workaround ---

                // Returns success with the potentially updated domain entity.
                return Result<FailedLoginAttemptEntity>.Success(entity);
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "DB error creating failed attempt record: {DbError}", dbEx.InnerException?.Message ?? dbEx.Message); 
                return Result<FailedLoginAttemptEntity>.Failure("DB error creating failed attempt record.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating failed attempt record: {Error}", ex.Message); 
                return Result<FailedLoginAttemptEntity>.Failure("Error creating failed attempt record.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<bool>> Update(FailedLoginAttemptEntity entity)
        {
            // Basic null check.
            if (entity == null) return Result<bool>.Failure("Failed attempt entity cannot be null.");
            try
            {
                // Finds existing entity (tracked).
                var existing = await _context.Failedloginattempt.FindAsync(entity.IdFailedLoginAttempt);
                // Returns failure if not found.
                if (existing == null) return Result<bool>.Failure("Failed attempt not found for update.");

                // Maps domain entity onto existing tracked entity.
                MapToDbModel(entity, existing);

                // Marks for update and saves.
                _context.Failedloginattempt.Update(existing);
                int rows = await _context.SaveChangesAsync();
                // Returns success based on rows affected.
                return rows > 0 ? Result<bool>.Success(true) : Result<bool>.Failure("No changes were saved for the failed attempt record.");
            }
            catch (DbUpdateConcurrencyException ex) // Handles concurrency conflicts.
            {
                _logger.LogError(ex, "Concurrency error updating failed attempt ID {FailedAttemptId}", entity.IdFailedLoginAttempt); 
                return Result<bool>.Failure("Concurrency error updating failed attempt ID.");
            }
            catch (DbUpdateException dbEx) // Handles other DB update errors.
            {
                _logger.LogError(dbEx, "DB error updating failed attempt record: {DbError}", dbEx.InnerException?.Message ?? dbEx.Message); 
                return Result<bool>.Failure("DB error updating failed attempt record.");
            }
            catch (Exception ex) // Handles general errors.
            {
                _logger.LogError(ex, "Error updating failed attempt record: {Error}", ex.Message); 
                return Result<bool>.Failure("Error updating failed attempt record.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<bool>> Delete(int id)
        {
            try
            {
                // Finds entity by ID (tracked).
                var attempt = await _context.Failedloginattempt.FindAsync(id);
                // Returns failure if not found.
                if (attempt == null) return Result<bool>.Failure("Failed attempt ID not found for deletion.");

                // Removes from context and saves.
                _context.Failedloginattempt.Remove(attempt);
                int rows = await _context.SaveChangesAsync();
                // Returns success based on rows affected.
                return rows > 0 ? Result<bool>.Success(true) : Result<bool>.Failure("Failed to delete the failed attempt record (no rows affected).");
            }
            catch (DbUpdateException dbEx) // Handles DB deletion errors.
            {
                _logger.LogError(dbEx, "DB error deleting failed attempt record: {DbError}", dbEx.InnerException?.Message ?? dbEx.Message); 
                return Result<bool>.Failure("DB error deleting failed attempt record.");
            }
            catch (Exception ex) // Handles general errors.
            {
                _logger.LogError(ex, "Error deleting failed attempt record: {Error}", ex.Message); 
                return Result<bool>.Failure("Error deleting failed attempt record.");
            }
        }
    }
}