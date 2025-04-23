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

        // Static fields for storing status IDs once initialized.
        private static int _pendingStatusId;
        private static int _usedStatusId;
        private static int _expiredStatusId;
        private static int _revokedStatusId;
        /// <summary>Flag indicating if status IDs have been successfully initialized.</summary>
        private static volatile bool _statusesInitialized; // Removed '= false' initializer
        /// <summary>Semaphore for thread-safe lazy initialization of status IDs.</summary>
        private static readonly SemaphoreSlim _initLock = new(1, 1);

        // Constants for status names used for initialization lookup.
        private const string PENDING_STATUS_NAME = "pendiente";
        private const string USED_STATUS_NAME = "usada";
        private const string EXPIRED_STATUS_NAME = "expirada";
        private const string REVOKED_STATUS_NAME = "revocada";
        private const string TABLE_NAME = "cropinvitation";

        /// <inheritdoc/>
        public async Task<Result<bool>> MarkAsUsedAsync(int invitationId, int userId)
        {
            // Ensures status IDs are loaded before proceeding.
            if (!await EnsureStatusesInitializedAsync())
            {
                return Result<bool>.Failure("Repository status initialization failed.");
            }
            // Check specific required statuses
            if (_usedStatusId == 0 || _pendingStatusId == 0)
            {
                _logger.LogError("Cannot mark invitation as used: Required statuses not initialized correctly (UsedStatusId={UsedStatusId}, PendingStatusId={PendingStatusId}).", _usedStatusId, _pendingStatusId);
                return Result<bool>.Failure("Required status IDs (Used, Pending) not available.");
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
                    return validationResult; // Validation failed, return its result.
                }

                // --- Use helper method for update logic ---
                return await PerformUsageUpdateAsync(invitation, userId);
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
            catch (Exception ex) // Catch unexpected errors
            {
                _logger.LogError(ex, "Unexpected error marking invitation {InvitationId} as used by User ID {UserId}.", invitationId, userId);
                return Result<bool>.Failure("Error marking invitation as used.");
            }
        }

        /// <summary>
        /// Performs the actual update to mark an invitation as used and saves changes.
        /// </summary>
        /// <param name="invitation">The tracked Cropinvitation entity.</param>
        /// <param name="userId">The ID of the user using the invitation.</param>
        /// <returns>Result indicating success or failure.</returns>
        private async Task<Result<bool>> PerformUsageUpdateAsync(Cropinvitation invitation, int userId)
        {
            // Updates the invitation properties.
            invitation.Usedby = userId;
            invitation.Statusid = _usedStatusId;

            // Saves the changes to the database.
            int rowsAffected = await _context.SaveChangesAsync();

            // Checks if the update was successful.
            if (rowsAffected > 0)
            {
                _logger.LogInformation("Successfully marked invitation {InvitationId} as used by User ID {UserId}.", invitation.Idcropinvitation, userId);
                return Result<bool>.Success(true);
            }

            _logger.LogWarning("No rows affected when attempting to mark invitation {InvitationId} as used for User ID {UserId}.", invitation.Idcropinvitation, userId);
            return Result<bool>.Failure("Failed to update invitation status, possibly due to a concurrency issue or unchanged data.");
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
            // Checks if the invitation has already been used (Defensive check).
            if (invitation.Usedby != null)
            {
                _logger.LogWarning("Attempted to mark invitation {InvitationId} as used, but it was already used by User ID {UsedById}.", invitation.Idcropinvitation, invitation.Usedby);
                return Result<bool>.Failure("Invitation has already been used.");
            }

            return Result<bool>.Success(true); // All checks passed
        }

        /// <summary>
        /// Ensures that the static status ID fields are initialized from the database.
        /// Uses lazy initialization with thread safety (<see cref="SemaphoreSlim"/>).
        /// </summary>
        /// <returns>True if initialized successfully, false otherwise.</returns>
        private async Task<bool> EnsureStatusesInitializedAsync()
        {
            // Double-check locking pattern
            if (_statusesInitialized) return true;
            await InitializeStatusIdsAsync(); // Call the main init logic
            return _statusesInitialized; // Return the final state
        }


        /// <summary>
        /// Initializes the static status ID fields (_pendingStatusId, etc.) from the database.
        /// Uses lazy initialization with thread safety (<see cref="SemaphoreSlim"/>).
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown indirectly if initialization fails critically inside.</exception>
        private async Task InitializeStatusIdsAsync()
        {
            // First check (no lock)
            if (_statusesInitialized) return;

            await _initLock.WaitAsync();
            try
            {
                // Second check (inside lock)
                if (_statusesInitialized) return;

                _logger.LogInformation("Initializing CropInvitation status IDs...");
                var statusResult = await _statusRepository.GetStatusesByTableNameAsync(TABLE_NAME);

                if (statusResult.IsFailure || !statusResult.Value.Any())
                {
                    _logger.LogError("Failed to retrieve required statuses for table '{TableName}'. Error: {ErrorMessage}",
                                     TABLE_NAME, statusResult.ErrorMessage ?? "No statuses found.");
                    // Throw or handle critical failure - keeping throw as repository cannot function
                    throw new InvalidOperationException($"Could not retrieve required statuses for {TABLE_NAME}. Error: {statusResult.ErrorMessage}");
                }

                // Assign IDs using helper
                foreach (var status in statusResult.Value)
                {
                    AssignStatusId(status);
                }

                // Validate assignment using helper
                if (!ValidateRequiredStatusesAssigned())
                {
                    // Logging moved inside ValidateRequiredStatusesAssigned
                    throw new InvalidOperationException($"Missing or failed to assign required status definitions for {TABLE_NAME}. Check status table data and names.");
                }

                _logger.LogInformation("Successfully initialized CropInvitation statuses: Pending={PendingId}, Used={UsedId}, Expired={ExpiredId}, Revoked={RevokedId}",
                                       _pendingStatusId, _usedStatusId, _expiredStatusId, _revokedStatusId);

                // Justification for setting static fields in instance method:
                // Required because initialization depends on instance services (_statusRepository, _logger).
                // Thread-safety handled by SemaphoreSlim and double-check lock. Performance impact is negligible.
                // Codacy warning for CRITICAL Performance is acknowledged but the pattern is necessary here.
                _statusesInitialized = true; // Mark as initialized
            }
            catch (Exception ex) // Catch broader exceptions during init
            {
                _statusesInitialized = false; // Ensure flag is false on error
                _logger.LogError(ex, "Fatal error during CropInvitation status initialization. Statuses remain uninitialized.");
                // Re-throw wrapped exception to signal critical failure
                throw new InvalidOperationException("Fatal error during status initialization.", ex);
            }
            finally
            {
                _initLock.Release();
            }
        }

        /// <summary>
        /// Assigns the status ID to the corresponding static field based on the status name.
        /// </summary>
        /// <param name="status">The StatusEntity containing the ID and name.</param>
        private void AssignStatusId(StatusEntity status)
        {
            switch (status.NameStatus.ToLowerInvariant())
            {
                case PENDING_STATUS_NAME: _pendingStatusId = status.IdStatus; break;
                case USED_STATUS_NAME: _usedStatusId = status.IdStatus; break;
                case EXPIRED_STATUS_NAME: _expiredStatusId = status.IdStatus; break;
                case REVOKED_STATUS_NAME: _revokedStatusId = status.IdStatus; break;
                default:
                    _logger.LogWarning("Encountered unexpected status '{StatusName}' (ID: {StatusId}) associated with table '{TableName}'. Ignoring.",
                                       status.NameStatus, status.IdStatus, TABLE_NAME);
                    break;
            }
        }

        /// <summary>
        /// Validates that all required static status ID fields have been assigned a non-zero value.
        /// </summary>
        /// <returns>True if all required IDs are assigned, false otherwise.</returns>
        private bool ValidateRequiredStatusesAssigned()
        {
            if (_pendingStatusId == 0 || _usedStatusId == 0 || _expiredStatusId == 0 || _revokedStatusId == 0)
            {
                _logger.LogError("One or more required statuses ('{Pending}', '{Used}', '{Expired}', '{Revoked}') were not found or assigned correctly for table '{TableName}'. Found IDs: Pending={PendingId}, Used={UsedId}, Expired={ExpiredId}, Revoked={RevokedId}",
                                 PENDING_STATUS_NAME, USED_STATUS_NAME, EXPIRED_STATUS_NAME, REVOKED_STATUS_NAME, TABLE_NAME,
                                 _pendingStatusId, _usedStatusId, _expiredStatusId, _revokedStatusId);
                return false;
            }
            return true;
        }

        // --- Mapping Methods ---
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

        private static Cropinvitation MapToDbModel(CropInvitationEntity entity, Cropinvitation? existingInvitation = null)
        {
            var invitation = existingInvitation ?? new Cropinvitation();

            // Only set ID if creating new - avoid changing PK on update
            if (existingInvitation == null)
            {
                // ID should be generated by DB, maybe remove this line if ID is identity column
                // invitation.Idcropinvitation = entity.IdCropInvitation;
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
            if (string.IsNullOrWhiteSpace(accessCode))
                return Result<CropInvitationEntity>.Failure("Access code cannot be empty.");

            if (!await EnsureStatusesInitializedAsync() || _pendingStatusId == 0)
            {
                _logger.LogError("Cannot get invitation by code: Repository statuses not initialized correctly (PendingStatusId={PendingStatusId}).", _pendingStatusId);
                return Result<CropInvitationEntity>.Failure("Repository not properly initialized.");
            }

            try
            {
                // --- Use helper for DB query and initial mapping ---
                var invitation = await FindActiveInvitationDbAsync(accessCode);

                // Map to domain entity after retrieval
                var domainEntity = MapToDomainEntity(invitation);
                if (domainEntity == null)
                    return Result<CropInvitationEntity>.Failure("Invitation code is invalid, expired, already used, or not found.");

                return Result<CropInvitationEntity>.Success(domainEntity);
            }
            catch (InvalidOperationException initEx) // Catch potential init error re-thrown
            {
                _logger.LogError(initEx, "Failed to get invitation by code due to initialization error: {AccessCode}", accessCode);
                return Result<CropInvitationEntity>.Failure($"Internal repository error: {initEx.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving active invitation by code: {AccessCode}", accessCode);
                return Result<CropInvitationEntity>.Failure("Error retrieving invitation by code.");
            }
        }

        /// <summary>
        /// Finds an active (pending, not expired, not used) invitation by access code in the database.
        /// </summary>
        /// <param name="accessCode">The access code to search for.</param>
        /// <returns>The database Cropinvitation entity or null if not found/inactive.</returns>
        private async Task<Cropinvitation?> FindActiveInvitationDbAsync(string accessCode)
        {
            var now = _datetime.GetUtcNow();
            // Query remains the same, complexity inherent to the query conditions
            return await _context.Cropinvitation
                                 .AsNoTracking()
                                 .FirstOrDefaultAsync(inv => inv.Accescode == accessCode
                                                        && inv.Expiresat > now
                                                        && inv.Statusid == _pendingStatusId
                                                        && inv.Usedby == null);
        }


        // --- Other Repository Methods (GetAll, GetById, Create, Update, Delete) ---
        // These methods seem okay regarding the reported issues, but review them for consistency.
        // Note: Create method might benefit from EnsureStatusesInitializedAsync call as well.
        // Update: Added EnsureStatusesInitializedAsync to Create.

        /// <inheritdoc/>
        public async Task<Result<CropInvitationEntity>> GetById(int id)
        {
            try
            {
                var invitation = await _context.Cropinvitation.AsNoTracking().FirstOrDefaultAsync(i => i.Idcropinvitation == id);
                var domainEntity = MapToDomainEntity(invitation);
                if (domainEntity == null) return Result<CropInvitationEntity>.Failure("Invitation not found.");
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
                var invitations = await _context.Cropinvitation.AsNoTracking().ToListAsync();
                return Result<IEnumerable<CropInvitationEntity>>.Success(invitations.Select(MapToDomainEntity).OfType<CropInvitationEntity>()); // Use OfType to filter potential nulls from MapToDomainEntity
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
            if (entity == null) return Result<CropInvitationEntity>.Failure("Invitation entity cannot be null.");

            // Ensure statuses are ready for setting default status
            if (!await EnsureStatusesInitializedAsync() || _pendingStatusId == 0)
            {
                _logger.LogError("Cannot create invitation: Repository statuses not initialized correctly (PendingStatusId={PendingStatusId}).", _pendingStatusId);
                return Result<CropInvitationEntity>.Failure("Repository not properly initialized.");
            }

            try
            {
                var dbModel = MapToDbModel(entity);
                if (dbModel.Createdat == default) dbModel.Createdat = _datetime.GetUtcNow();
                if (dbModel.Statusid == null || dbModel.Statusid == 0) dbModel.Statusid = _pendingStatusId;

                await _context.Cropinvitation.AddAsync(dbModel);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Successfully created invitation with ID {InvitationId}.", dbModel.Idcropinvitation);

                var createdEntity = MapToDomainEntity(dbModel);
                if (createdEntity == null)
                {
                    _logger.LogError("Failed to map newly created invitation back to domain entity. DB ID: {DbInvitationId}", dbModel.Idcropinvitation);
                    return Result<CropInvitationEntity>.Failure("Failed to map created invitation.");
                }
                return Result<CropInvitationEntity>.Success(createdEntity);
            }
            catch (InvalidOperationException initEx)
            {
                _logger.LogError(initEx, "Failed to create invitation due to initialization error. Input Entity: {@InvitationEntity}", entity);
                return Result<CropInvitationEntity>.Failure($"Internal repository error: {initEx.Message}");
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "DB error creating invitation. Input Entity: {@InvitationEntity}", entity);
                return Result<CropInvitationEntity>.Failure("DB error creating invitation.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating invitation. Input Entity: {@InvitationEntity}", entity);
                return Result<CropInvitationEntity>.Failure("Error creating invitation.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<bool>> Update(CropInvitationEntity entity)
        {
            if (entity == null) return Result<bool>.Failure("Invitation entity cannot be null.");
            if (entity.IdCropInvitation <= 0) return Result<bool>.Failure("Invalid ID provided for update.");

            try
            {
                var existing = await _context.Cropinvitation.FindAsync(entity.IdCropInvitation);
                if (existing == null)
                {
                    _logger.LogWarning("Invitation with ID {InvitationId} not found for update.", entity.IdCropInvitation);
                    return Result<bool>.Failure("Invitation not found for update.");
                }

                MapToDbModel(entity, existing);
                int rows = await _context.SaveChangesAsync();

                if (rows > 0)
                {
                    _logger.LogInformation("Successfully updated invitation ID: {InvitationId}", entity.IdCropInvitation);
                    return Result<bool>.Success(true);
                }
                _logger.LogInformation("No changes detected or saved for invitation ID: {InvitationId}. Entity data might be identical.", entity.IdCropInvitation);
                return Result<bool>.Success(false);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "Concurrency error updating invitation ID {InvitationId}.", entity.IdCropInvitation);
                return Result<bool>.Failure("Concurrency error updating invitation.");
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
            if (id <= 0) return Result<bool>.Failure("Invalid ID provided for deletion.");

            try
            {
                var invitation = await _context.Cropinvitation.FindAsync(id);
                if (invitation == null)
                {
                    _logger.LogWarning("Invitation with ID {InvitationId} not found for deletion.", id);
                    return Result<bool>.Failure("Invitation not found for deletion.");
                }

                _context.Cropinvitation.Remove(invitation);
                int rows = await _context.SaveChangesAsync();

                if (rows > 0)
                {
                    _logger.LogInformation("Successfully deleted invitation ID: {InvitationId}", id);
                    return Result<bool>.Success(true);
                }
                _logger.LogWarning("Failed to delete invitation ID (no rows affected): {InvitationId}", id);
                return Result<bool>.Failure("Deletion failed unexpectedly.");
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "DB error deleting invitation ID: {InvitationId}.", id);
                return Result<bool>.Failure("DB error deleting invitation.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting invitation ID: {InvitationId}", id);
                return Result<bool>.Failure("Error deleting invitation.");
            }
        }
    }
}
