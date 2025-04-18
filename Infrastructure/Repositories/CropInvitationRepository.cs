using ArandanoIRT_Backend.Application.Interfaces.Utilities;
using ArandanoIRT_Backend.Domain.Entities;
using ArandanoIRT_Backend.Domain.IRepositories;
using ArandanoIRT_Backend.Domain.ValueObjects;
using ArandanoIRT_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ArandanoIRT_Backend.Infrastructure.Repositories
{
    /// <summary>
    /// Implements the <see cref="ICropInvitationRepository"/> interface, providing data access logic
    /// for crop invitation entities (<see cref="CropInvitationEntity"/>) using Entity Framework Core.
    /// Includes lazy initialization for status IDs required by repository operations.
    /// </summary>
    public class CropInvitationRepository(ApplicationDbContext context, IStatusRepository statusRepository, IDateTimeProvider dateTimeProvider, ILogger<CropInvitationRepository> logger) : ICropInvitationRepository
    {
        private readonly ApplicationDbContext _context = context ?? throw new ArgumentNullException(nameof(context));
        private readonly IStatusRepository _statusRepository = statusRepository ?? throw new ArgumentNullException(nameof(statusRepository));
        private readonly IDateTimeProvider _datetime = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
        /// <summary>
        /// Logger instance for logging repository operations and errors.
        /// </summary>
        private readonly ILogger<CropInvitationRepository> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        /// <summary>ID for the 'Pending' status.</summary>
        private static int _pendingStatusId;
        /// <summary>ID for the 'Used' status.</summary>
        private static int _usedStatusId;
        /// <summary>ID for the 'Expired' status.</summary>
        private static int _expiredStatusId;
        /// <summary>ID for the 'Revoked' status.</summary>
        private static int _revokedStatusId;
        /// <summary>Flag indicating if status IDs have been successfully initialized.</summary>
        private static volatile bool _statusesInitialized = false; 
        /// <summary>Semaphore for thread-safe lazy initialization of status IDs.</summary>
        private static readonly SemaphoreSlim _initLock = new(1, 1);

        // Constants for status names used for initialization lookup.
        /// <summary>Name constant for the 'Pending' status.</summary>
        private const string PENDING_STATUS_NAME = "pendiente";
        /// <summary>Name constant for the 'Used' status.</summary>
        private const string USED_STATUS_NAME = "usada";
        /// <summary>Name constant for the 'Expired' status.</summary>
        private const string EXPIRED_STATUS_NAME = "expirada";
        /// <summary>Name constant for the 'Revoked' status.</summary>
        private const string REVOKED_STATUS_NAME = "revocada";
        /// <summary>Table name constant used for retrieving relevant statuses.</summary>
        private const string TABLE_NAME = "cropinvitation";

        /// <inheritdoc/>
        public async Task<Result<bool>> MarkAsUsedAsync(int invitationId, int userId)
        {
            // Ensures status IDs are loaded before proceeding.
            await InitializeStatusIdsAsync();
            // Checks if initialization was successful and required IDs are set.
            // Check for UsedStatusId and PendingStatusId as they are needed here.
            if (!_statusesInitialized || _usedStatusId == 0 || _pendingStatusId == 0)
            {
                _logger.LogError("Cannot mark invitation as used: Repository statuses not initialized correctly (UsedStatusId={UsedStatusId}, PendingStatusId={PendingStatusId}).", _usedStatusId, _pendingStatusId);
                return Result<bool>.Failure("Repository status initialization failed.");
            }

            // Validates the user ID input.
            if (userId <= 0)
            {
                _logger.LogWarning("Attempt to mark invitation {InvitationId} as used with invalid User ID {UserId}", invitationId, userId);
                return Result<bool>.Failure("Invalid user ID provided.");
            }

            try
            {
                // Finds the specific invitation, *tracking* it for update.
                var invitation = await _context.Cropinvitation.FirstOrDefaultAsync(inv => inv.Idcropinvitation == invitationId);

                // Checks if the invitation exists.
                if (invitation == null)
                {
                    _logger.LogWarning("Attempt to mark non-existent invitation {InvitationId} as used.", invitationId);
                    return Result<bool>.Failure("Invitation not found.");
                }

                // --- Use helper method for validation ---
                var validationResult = ValidateInvitationForUsage(invitation);
                if (validationResult.IsFailure)
                {
                    // Error already logged within validation method if necessary.
                    return validationResult; // Return the failure result from validation.
                }
                // --- End validation ---

                // Updates the invitation properties.
                invitation.Usedby = userId; // Associates the user who used the invitation.
                invitation.Statusid = _usedStatusId; // Sets status to 'used'.

                // Saves the changes to the database.
                int rowsAffected = await _context.SaveChangesAsync();

                // Checks if the update was successful.
                if (rowsAffected > 0)
                {
                    _logger.LogInformation("Successfully marked invitation {InvitationId} as used by User ID {UserId}.", invitationId, userId);
                    return Result<bool>.Success(true);
                }
                // Logs a warning if no rows were affected (e.g., concurrency issue or data didn't actually change).
                _logger.LogWarning("No rows affected when attempting to mark invitation {InvitationId} as used for User ID {UserId}.", invitationId, userId);
                // Failure seems more appropriate if an update was expected but didn't happen.
                return Result<bool>.Failure("Failed to update invitation status, possibly due to a concurrency issue or unchanged data.");
            }
            catch (DbUpdateConcurrencyException concEx) 
            {
                _logger.LogWarning(concEx, "Concurrency error marking invitation {InvitationId} as used by User ID {UserId}.", invitationId, userId);
                return Result<bool>.Failure("Could not update invitation due to a concurrency conflict.");
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error marking invitation {InvitationId} as used by User ID {UserId}.", invitationId, userId);
                return Result<bool>.Failure("Database error marking invitation as used."); 
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error marking invitation {InvitationId} as used by User ID {UserId}.", invitationId, userId);
                return Result<bool>.Failure("Error marking invitation as used.");
            }
        }

        /// <summary>
        /// Validates if a crop invitation is in a state suitable for being marked as used.
        /// Assumes status IDs are initialized.
        /// </summary>
        /// <param name="invitation">The database invitation entity to validate.</param>
        /// <returns>A Result indicating success (true) if valid, or failure with an error message.</returns>
        private Result<bool> ValidateInvitationForUsage(Cropinvitation invitation)
        {

            // Checks if the invitation is currently in the 'Pending' state.
            if (invitation.Statusid != _pendingStatusId)
            {
                _logger.LogWarning("Attempted to mark invitation {InvitationId} as used, but its current status ID is {StatusId} (Expected: {PendingStatusId})",
                                invitation.Idcropinvitation, invitation.Statusid, _pendingStatusId);
                return Result<bool>.Failure("Invitation is not in a valid state to be marked as used.");
            }
            // Checks if the invitation has expired.
            if (invitation.Expiresat <= _datetime.GetUtcNow()) 
            {
                _logger.LogWarning("Attempted to mark invitation {InvitationId} as used, but it has expired ({ExpiryDate}).", invitation.Idcropinvitation, invitation.Expiresat);
                return Result<bool>.Failure("Invitation has expired.");
            }
            // Checks if the invitation has already been used (Defensive check, should be covered by Pending status).
            if (invitation.Usedby != null)
            {
                _logger.LogWarning("Attempted to mark invitation {InvitationId} as used, but it was already used by User ID {UsedById}.", invitation.Idcropinvitation, invitation.Usedby);
                return Result<bool>.Failure("Invitation has already been used.");
            }

            // If all checks pass
            return Result<bool>.Success(true);
        }

        /// <summary>
        /// Ensures that the static status ID fields are initialized from the database.
        /// Uses lazy initialization with thread safety (<see cref="SemaphoreSlim"/>).
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if required statuses cannot be retrieved, are missing, or initialization fails.</exception>
        private async Task InitializeStatusIdsAsync()
        {
            // Double-check locking pattern (first check avoids lock contention if already initialized).
            if (_statusesInitialized) return;

            // Acquires the lock to ensure only one thread initializes.
            await _initLock.WaitAsync();
            try
            {
                // Second check inside the lock to handle race condition.
                if (_statusesInitialized) return;

                _logger.LogInformation("Initializing CropInvitation status IDs...");
                // Retrieves statuses related to the 'cropinvitation' table.
                var statusResult = await _statusRepository.GetStatusesByTableNameAsync(TABLE_NAME);
                // Checks if retrieval failed or returned no results.
                if (statusResult.IsFailure || !statusResult.Value.Any())
                {
                    _logger.LogError("Failed to retrieve required statuses for table '{TableName}'. Error: {ErrorMessage}",
                                    TABLE_NAME, statusResult.ErrorMessage ?? "No statuses found.");
                    // Throws if initialization fails, as repository cannot function correctly without status IDs.
                    throw new InvalidOperationException($"Could not initialize required statuses for {TABLE_NAME}. Repository cannot function correctly. Error: {statusResult.ErrorMessage}");
                }

                // Maps status names (case-insensitive) to static ID fields.
                foreach (var status in statusResult.Value)
                {
                    switch (status.NameStatus.ToLowerInvariant()) 
                    {
                        case PENDING_STATUS_NAME: _pendingStatusId = status.IdStatus; break;
                        case USED_STATUS_NAME: _usedStatusId = status.IdStatus; break;
                        case EXPIRED_STATUS_NAME: _expiredStatusId = status.IdStatus; break;
                        case REVOKED_STATUS_NAME: _revokedStatusId = status.IdStatus; break;
                        default: 
                            // Log unexpected status names associated with this table.
                            _logger.LogWarning("Encountered unexpected status '{StatusName}' (ID: {StatusId}) associated with table '{TableName}'. Ignoring.",
                                               status.NameStatus, status.IdStatus, TABLE_NAME);
                            break; 
                    }
                }

                // Validates that all required status IDs were found and assigned.
                if (_pendingStatusId == 0 || _usedStatusId == 0 || _expiredStatusId == 0 || _revokedStatusId == 0)
                {
                    _logger.LogError("One or more required statuses ('{Pending}', '{Used}', '{Expired}', '{Revoked}') were not found or assigned correctly for table '{TableName}'. Found IDs: Pending={PendingId}, Used={UsedId}, Expired={ExpiredId}, Revoked={RevokedId}",
                                    PENDING_STATUS_NAME, USED_STATUS_NAME, EXPIRED_STATUS_NAME, REVOKED_STATUS_NAME, TABLE_NAME,
                                    _pendingStatusId, _usedStatusId, _expiredStatusId, _revokedStatusId); 
                    throw new InvalidOperationException($"Missing or failed to assign required status definitions for {TABLE_NAME}. Check status table data and names.");
                }
                else
                {
                    // Logs successful initialization.
                    _logger.LogInformation("Successfully initialized CropInvitation statuses: Pending={PendingId}, Used={UsedId}, Expired={ExpiredId}, Revoked={RevokedId}",
                                       _pendingStatusId, _usedStatusId, _expiredStatusId, _revokedStatusId);

                    // Sets the flag indicating successful initialization.
                    // <<< CRITICAL/PERFORMANCE NOTE: Justification for non-static >>>
                    // This instance method modifies static fields (_pendingStatusId, etc., _statusesInitialized).
                    // Codacy warns about this. However, this method *cannot* be static because it depends on
                    // instance-injected services (_statusRepository, _logger) obtained via the constructor.
                    // The logic inherently relates to initializing shared state for the repository type.
                    // Thread safety for this lazy initialization is correctly handled using SemaphoreSlim (_initLock)
                    // and the double-check locking pattern. The performance impact of assignment is negligible.
                    _statusesInitialized = true; // Keep assignment here.
                }
            }
            catch (Exception ex) 
            {
                // Ensure _statusesInitialized is false if we exit due to error
                _statusesInitialized = false;
                _logger.LogError(ex, "Fatal error during CropInvitation status initialization. Statuses remain uninitialized.");
                // Wrap and re-throw to indicate critical failure clearly.
                throw new InvalidOperationException("Fatal error during status initialization.", ex);
            }
            finally
            {
                // Releases the semaphore lock.
                _initLock.Release();
            }
        }

        /// <summary>
        /// Maps a database context <see cref="Cropinvitation"/> entity to a domain <see cref="CropInvitationEntity"/>.
        /// </summary>
        /// <param name="invitation">The database entity instance.</param>
        /// <returns>The mapped domain entity instance, or <c>null</c> if input is null.</returns>
        /// <remarks>Handles potential null Cropid from DB by defaulting to 0. Maps DB 'Accescode' to domain 'AccessCode'.</remarks>
        private CropInvitationEntity? MapToDomainEntity(Cropinvitation? invitation)
        {
            if (invitation == null) return null;
            return new CropInvitationEntity(
                invitation.Idcropinvitation,
                invitation.Accescode, 
                invitation.Createdat,
                invitation.Expiresat,
                invitation.Statusid ?? 0, 
                invitation.Createdby,
                invitation.Usedby,
                invitation.Cropid ?? 0 
            );
        }

        /// <summary>
        /// Maps a domain <see cref="CropInvitationEntity"/> to a database context <see cref="Cropinvitation"/> entity.
        /// Updates existing instance if provided, otherwise creates a new one.
        /// </summary>
        /// <param name="entity">The domain entity instance.</param>
        /// <param name="existingInvitation">Optional. The existing database entity to update.</param>
        /// <returns>The mapped or updated database entity instance.</returns>
        /// <remarks>Maps domain 'AccessCode' to DB 'Accescode' (potential typo in DB schema).</remarks>
        private static Cropinvitation MapToDbModel(CropInvitationEntity entity, Cropinvitation? existingInvitation = null)
        {
            var invitation = existingInvitation ?? new Cropinvitation();

            if (existingInvitation != null)
            {
                invitation.Idcropinvitation = entity.IdCropInvitation;
            }

            invitation.Accescode = entity.AccessCode;
            invitation.Createdat = entity.CreatedAt;
            invitation.Expiresat = entity.ExpiresAt;
            invitation.Statusid = entity.StatusId;
            invitation.Createdby = entity.CreatedBy;
            invitation.Usedby = entity.UsedBy;
            invitation.Cropid = entity.CropId; 
            return invitation;
        }

        /// <inheritdoc/>
        public async Task<Result<CropInvitationEntity>> GetActiveByCodeAsync(string accessCode)
        {
            // Ensures statuses are loaded for query conditions.
            await InitializeStatusIdsAsync();

            // Basic input validation.
            if (string.IsNullOrWhiteSpace(accessCode))
                return Result<CropInvitationEntity>.Failure("Access code cannot be empty.");
            // Checks if repository is ready.
            if (!_statusesInitialized || _pendingStatusId == 0) // Check only for needed status ID
            {
                _logger.LogError("Cannot get invitation by code: Repository statuses not initialized correctly (PendingStatusId={PendingStatusId}).", _pendingStatusId);
                return Result<CropInvitationEntity>.Failure("Repository not properly initialized. Cannot verify invitation status.");
            }

            try
            {
                // Gets current time for expiration check.
                var now = _datetime.GetUtcNow();

                // Queries for an invitation matching the code, not expired, and in 'pending' state. Uses AsNoTracking.
                var invitation = await _context.Cropinvitation
                                               .AsNoTracking()
                                               .FirstOrDefaultAsync(inv => inv.Accescode == accessCode // Uses DB 'Accescode' typo
                                                                      && inv.Expiresat > now
                                                                      && inv.Statusid == _pendingStatusId
                                                                      && inv.Usedby == null); // Explicitly check Usedby is null

                // Returns failure if no matching active invitation is found.
                var domainEntity = MapToDomainEntity(invitation); // Map once
                if (domainEntity == null)
                    return Result<CropInvitationEntity>.Failure("Invitation code is invalid, expired, already used, or not found.");

                // Maps the found DB entity to domain entity and returns success.
                return Result<CropInvitationEntity>.Success(domainEntity);
            }
            catch (InvalidOperationException initEx)
            {
                _logger.LogError(initEx, "Failed to get invitation by code due to initialization error: {AccessCode}", accessCode);
                return Result<CropInvitationEntity>.Failure($"Internal repository error during initialization: {initEx.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving active invitation by code: {AccessCode}", accessCode);
                return Result<CropInvitationEntity>.Failure("Error retrieving invitation by code.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<CropInvitationEntity>> GetById(int id)
        {
            try
            {
                // Finds invitation by PK using AsNoTracking.
                var invitation = await _context.Cropinvitation.AsNoTracking().FirstOrDefaultAsync(i => i.Idcropinvitation == id);

                var domainEntity = MapToDomainEntity(invitation); 
                // Returns failure if not found.
                if (domainEntity == null) return Result<CropInvitationEntity>.Failure("Invitation not found.");
                // Maps and returns success.
                return Result<CropInvitationEntity>.Success(domainEntity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving invitation by ID: {InvitationId}", id);
                return Result<CropInvitationEntity>.Failure("Error retrieving invitation.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<IEnumerable<CropInvitationEntity>>> GetAll()
        {
            try
            {
                // Retrieves all invitations using AsNoTracking.
                var invitations = await _context.Cropinvitation.AsNoTracking().ToListAsync();
                // Maps the list and returns success. Handle potential nulls from MapToDomainEntity if strict needed.
                return Result<IEnumerable<CropInvitationEntity>>.Success(invitations.Select(MapToDomainEntity).OfType<CropInvitationEntity>()); // Use OfType to filter potential nulls
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all invitations");
                return Result<IEnumerable<CropInvitationEntity>>.Failure("Error retrieving all invitations.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<CropInvitationEntity>> Create(CropInvitationEntity entity)
        {
            // Ensures status IDs are loaded for setting default status.
            await InitializeStatusIdsAsync();

            // Basic entity null check.
            if (entity == null) return Result<CropInvitationEntity>.Failure("Invitation entity cannot be null.");
            // Checks if repository is ready to set default status.
            if (!_statusesInitialized || _pendingStatusId == 0)
            {
                _logger.LogError("Cannot create invitation: Repository statuses not initialized correctly (PendingStatusId={PendingStatusId}).", _pendingStatusId);
                return Result<CropInvitationEntity>.Failure("Repository not properly initialized. Cannot set default invitation status.");
            }

            try
            {
                // Maps domain entity to DB model (creates new instance).
                var dbModel = MapToDbModel(entity);
                // Sets default CreatedAt timestamp if not provided by domain entity.
                if (dbModel.Createdat == default) dbModel.Createdat = _datetime.GetUtcNow();
                // Sets default StatusId to 'pending' if not provided or is 0 (assuming 0 is invalid/default).
                if (dbModel.Statusid == null || dbModel.Statusid == 0) dbModel.Statusid = _pendingStatusId;

                // Adds the new model to the context.
                await _context.Cropinvitation.AddAsync(dbModel);
                // Saves changes to generate the ID.
                await _context.SaveChangesAsync();

                // Log creation success
                _logger.LogInformation("Successfully created invitation with ID {InvitationId}.", dbModel.Idcropinvitation);

                // This avoids the complex/fragile reflection hack to update the input entity's ID.
                var createdEntity = MapToDomainEntity(dbModel);
                if (createdEntity == null) // Should not happen if dbModel is valid after save, but defensive check
                {
                    _logger.LogError("Failed to map newly created invitation back to domain entity. DB ID: {DbInvitationId}", dbModel.Idcropinvitation);
                    return Result<CropInvitationEntity>.Failure("Failed to map created invitation.");
                }
                return Result<CropInvitationEntity>.Success(createdEntity);
            }
            catch (InvalidOperationException initEx) 
            {
                _logger.LogError(initEx, "Failed to create invitation due to initialization error. Input Entity: {@InvitationEntity}", entity);
                return Result<CropInvitationEntity>.Failure($"Internal repository error during initialization: {initEx.Message}");
            }
            catch (DbUpdateException dbEx)
            {
                // Logs DB errors with structured entity data for context.
                _logger.LogError(dbEx, "DB error creating invitation. Input Entity: {@InvitationEntity}", entity);
                return Result<CropInvitationEntity>.Failure("DB error creating invitation.");
            }
            catch (Exception ex)
            {
                // Logs general errors with structured entity data.
                _logger.LogError(ex, "Error creating invitation. Input Entity: {@InvitationEntity}", entity);
                return Result<CropInvitationEntity>.Failure("Error creating invitation.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<bool>> Update(CropInvitationEntity entity)
        {
            // Basic entity null check.
            if (entity == null) return Result<bool>.Failure("Invitation entity cannot be null.");
            // ID check - cannot update entity without ID.
            if (entity.IdCropInvitation <= 0) return Result<bool>.Failure("Invalid ID provided for update.");

            try
            {
                // Finds the existing entity by ID using FindAsync (tracked).
                var existing = await _context.Cropinvitation.FindAsync(entity.IdCropInvitation);
                // Returns failure if not found.
                if (existing == null)
                {
                    _logger.LogWarning("Invitation with ID {InvitationId} not found for update.", entity.IdCropInvitation);
                    return Result<bool>.Failure("Invitation not found for update.");
                }

                // Maps properties from domain entity onto the tracked database entity.
                MapToDbModel(entity, existing);

                // Saves changes.
                int rows = await _context.SaveChangesAsync();

                // Check if rows were affected. 0 rows might mean the data was identical.
                if (rows > 0)
                {
                    _logger.LogInformation("Successfully updated invitation ID: {InvitationId}", entity.IdCropInvitation);
                    return Result<bool>.Success(true);
                }
                // This isn't necessarily an error, could mean no changes were detected.
                _logger.LogInformation("No changes detected or saved for invitation ID: {InvitationId}. Entity data might be identical.", entity.IdCropInvitation);
                return Result<bool>.Success(false); 
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "Concurrency error updating invitation ID {InvitationId}. The data may have been changed by another user.", entity.IdCropInvitation);
                return Result<bool>.Failure("Concurrency error updating invitation. Please refresh data and try again.");
            }
            catch (DbUpdateException dbEx) 
            {
                _logger.LogError(dbEx, "DB error updating invitation ID: {InvitationId}. Input Entity: {@InvitationEntity}", entity.IdCropInvitation, entity);
                return Result<bool>.Failure("DB error updating invitation.");
            }
            catch (Exception ex) 
            {
                _logger.LogError(ex, "Error updating invitation ID: {InvitationId}. Input Entity: {@InvitationEntity}", entity.IdCropInvitation, entity);
                return Result<bool>.Failure("Error updating invitation.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<bool>> Delete(int id)
        {
            // Basic ID validation
            if (id <= 0) return Result<bool>.Failure("Invalid ID provided for deletion.");

            try
            {
                // Finds the entity by ID using FindAsync (tracked).
                var invitation = await _context.Cropinvitation.FindAsync(id);
                // Returns failure if not found.
                if (invitation == null)
                {
                    _logger.LogWarning("Invitation with ID {InvitationId} not found for deletion.", id);
                    return Result<bool>.Failure("Invitation not found for deletion.");
                }

                // Removes the entity from the context.
                _context.Cropinvitation.Remove(invitation);
                // Saves changes.
                int rows = await _context.SaveChangesAsync();

                // Check if deletion was successful.
                if (rows > 0)
                {
                    _logger.LogInformation("Successfully deleted invitation ID: {InvitationId}", id);
                    return Result<bool>.Success(true);
                }
                // This is unexpected if the entity was found and Remove was called.
                _logger.LogWarning("Failed to delete invitation ID (no rows affected after Remove call): {InvitationId}", id);
                return Result<bool>.Failure("Deletion failed unexpectedly after finding the entity.");
            }
            catch (DbUpdateException dbEx) 
            {
                _logger.LogError(dbEx, "DB error deleting invitation ID: {InvitationId}. Check for related data.", id);
                return Result<bool>.Failure("DB error deleting invitation. It might be referenced by other data.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting invitation ID: {InvitationId}", id);
                return Result<bool>.Failure("Error deleting invitation.");
            }
        }
    }
}
