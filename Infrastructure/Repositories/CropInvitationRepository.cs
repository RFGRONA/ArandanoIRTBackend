using ArandanoIRT_Backend.Application.Interfaces.Utilities;
using ArandanoIRT_Backend.Domain.Entities;
using ArandanoIRT_Backend.Domain.IRepositories;
using ArandanoIRT_Backend.Domain.ValueObjects;
using ArandanoIRT_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace ArandanoIRT_Backend.Infrastructure.Repositories
{
    /// <summary>
    /// Implements the <see cref="ICropInvitationRepository"/> interface, providing data access logic
    /// for crop invitation entities (<see cref="CropInvitationEntity"/>) using Entity Framework Core.
    /// Includes lazy initialization for status IDs required by repository operations.
    /// </summary>
    public class CropInvitationRepository(ApplicationDbContext context, IStatusRepository statusRepository, IDateTimeProvider dateTimeProvider) : ICropInvitationRepository
    {
        private readonly ApplicationDbContext _context = context ?? throw new ArgumentNullException(nameof(context));
        private readonly IStatusRepository _statusRepository = statusRepository ?? throw new ArgumentNullException(nameof(statusRepository));
        private readonly IDateTimeProvider _datetime = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
        /// <summary>
        /// Static Serilog logger instance specific to this repository.
        /// </summary>
        private readonly Serilog.ILogger _logger = Log.ForContext<CropInvitationRepository>();

        // Status IDs - initialized lazily using InitializeStatusIdsAsync.
        /// <summary>ID for the 'Pending' status.</summary>
        private static int _pendingStatusId;
        /// <summary>ID for the 'Used' status.</summary>
        private static int _usedStatusId;
        /// <summary>ID for the 'Expired' status.</summary>
        private static int _expiredStatusId;
        /// <summary>ID for the 'Revoked' status.</summary>
        private static int _revokedStatusId;
        /// <summary>Flag indicating if status IDs have been successfully initialized.</summary>
        private static bool _statusesInitialized = false;
        /// <summary>Semaphore for thread-safe lazy initialization of status IDs.</summary>
        private static readonly SemaphoreSlim _initLock = new(1, 1);

        // Constants for status names used for initialization lookup.
        // Ensure these names match the data in the Statuses table (case-insensitive comparison is used).
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
            if (!_statusesInitialized || _usedStatusId == 0 || _pendingStatusId == 0) // Need pending to check current state
            {
                _logger.Error("Cannot mark invitation as used: Repository statuses not initialized correctly (UsedStatusId={UsedStatusId}, PendingStatusId={PendingStatusId}).", _usedStatusId, _pendingStatusId);
                return Result<bool>.Failure("Repository status initialization failed.");
            }

            // Validates the user ID input.
            if (userId <= 0)
            {
                return Result<bool>.Failure("Invalid user ID provided.");
            }

            try
            {
                // Finds the specific invitation, *tracking* it for update.
                var invitation = await _context.Cropinvitation.FirstOrDefaultAsync(inv => inv.Idcropinvitation == invitationId);

                // Checks if the invitation exists.
                if (invitation == null)
                {
                    return Result<bool>.Failure("Invitation not found.");
                }

                // --- Pre-update validation checks ---
                // Checks if the invitation is currently in the 'Pending' state.
                if (invitation.Statusid != _pendingStatusId)
                {
                    _logger.Warning("Attempted to mark invitation {InvitationId} as used, but its current status ID is {StatusId} (Expected: {PendingStatusId})",
                                    invitationId, invitation.Statusid, _pendingStatusId);
                    // Fails if the invitation is not in the correct state to be marked as used.
                    return Result<bool>.Failure("Invitation is not in a valid state to be marked as used.");
                }
                // Checks if the invitation has expired.
                if (invitation.Expiresat <= _datetime.GetUtcNow()) // Uses timestamp for comparison.
                {
                    _logger.Warning("Attempted to mark invitation {InvitationId} as used, but it has expired ({ExpiryDate}).", invitationId, invitation.Expiresat);
                    return Result<bool>.Failure("Invitation has expired.");
                }
                // Checks if the invitation has already been used.
                if (invitation.Usedby != null)
                {
                    _logger.Warning("Attempted to mark invitation {InvitationId} as used, but it was already used by User ID {UsedById}.", invitationId, invitation.Usedby);
                    return Result<bool>.Failure("Invitation has already been used.");
                }
                // --- End validation checks ---

                // Updates the invitation properties.
                invitation.Usedby = userId; // Associates the user who used the invitation.
                invitation.Statusid = _usedStatusId; // Sets status to 'used'.

                // EF Core tracks the changes to 'invitation' since it was retrieved without AsNoTracking().
                // Saves the changes to the database.
                int rowsAffected = await _context.SaveChangesAsync();

                // Checks if the update was successful.
                if (rowsAffected > 0)
                {
                    _logger.Information("Successfully marked invitation {InvitationId} as used by User ID {UserId}.", invitationId, userId);
                    return Result<bool>.Success(true);
                }
                else
                {
                    // Logs a warning if no rows were affected (e.g., concurrency issue).
                    _logger.Warning("No rows affected when marking invitation {InvitationId} as used for User ID {UserId}.", invitationId, userId);
                    return Result<bool>.Failure("Failed to update invitation status, possibly due to a concurrency issue.");
                }
            }
            catch (DbUpdateException dbEx)
            {
                _logger.Error(dbEx, "Database error marking invitation {InvitationId} as used by User ID {UserId}.", invitationId, userId);
                return Result<bool>.Failure("Database error marking invitation as use.");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error marking invitation {InvitationId} as used by User ID {UserId}.", invitationId, userId);
                return Result<bool>.Failure("Error marking invitation as used.");
            }
        }

        /// <summary>
        /// Ensures that the static status ID fields are initialized from the database.
        /// Uses lazy initialization with thread safety (<see cref="SemaphoreSlim"/>).
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if required statuses cannot be retrieved or are missing.</exception>
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

                _logger.Information("Initializing CropInvitation status IDs...");
                // Retrieves statuses related to the 'cropinvitation' table.
                var statusResult = await _statusRepository.GetStatusesByTableNameAsync(TABLE_NAME);
                // Checks if retrieval failed or returned no results.
                if (statusResult.IsFailure || !statusResult.Value.Any())
                {
                    _logger.Error("Failed to retrieve required statuses for table '{TableName}'. Error: {ErrorMessage}",
                                    TABLE_NAME, statusResult.ErrorMessage ?? "No statuses found.");
                    // Throws if initialization fails, as repository cannot function correctly without status IDs.
                    throw new InvalidOperationException($"Could not initialize required statuses for {TABLE_NAME}. Repository cannot function correctly.");
                }

                // Maps status names (case-insensitive) to static ID fields.
                foreach (var status in statusResult.Value)
                {
                    switch (status.NameStatus.ToLowerInvariant()) // Uses lowercase comparison.
                    {
                        case PENDING_STATUS_NAME: _pendingStatusId = status.IdStatus; break;
                        case USED_STATUS_NAME: _usedStatusId = status.IdStatus; break;
                        case EXPIRED_STATUS_NAME: _expiredStatusId = status.IdStatus; break;
                        case REVOKED_STATUS_NAME: _revokedStatusId = status.IdStatus; break;
                    }
                }

                // Validates that all required status IDs were found and assigned.
                if (_pendingStatusId == 0 || _usedStatusId == 0 || _expiredStatusId == 0 || _revokedStatusId == 0)
                {
                    _logger.Warning("One or more required statuses ('{Pending}', '{Used}', '{Expired}', '{Revoked}') were not found for table '{TableName}'.",
                                    PENDING_STATUS_NAME, USED_STATUS_NAME, EXPIRED_STATUS_NAME, REVOKED_STATUS_NAME, TABLE_NAME); // Updated Expired/Revoked names
                    throw new InvalidOperationException($"Missing required status definitions for {TABLE_NAME}.");
                }
                else
                {
                    // Logs successful initialization.
                    _logger.Information("Successfully initialized CropInvitation statuses: Pending={PendingId}, Used={UsedId}, Expired={ExpiredId}, Revoked={RevokedId}",
                                       _pendingStatusId, _usedStatusId, _expiredStatusId, _revokedStatusId);
                    // Sets the flag indicating successful initialization.
                    _statusesInitialized = true;
                }
            }
            catch (Exception ex) // Catches any unexpected error during initialization.
            {
                _logger.Fatal(ex, "Fatal error during CropInvitation status initialization.");
                throw; // Re-throws to indicate critical failure.
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
        /// <returns>The mapped domain entity instance, or <c>null</c> if input is null (throws NullReferenceException due to null! usage).</returns>
        /// <remarks>Handles potential null Cropid from DB by defaulting to 0. Maps DB 'Accescode' to domain 'AccessCode'.</remarks>
        private CropInvitationEntity MapToDomainEntity(Cropinvitation invitation)
        {
            if (invitation == null) return null!;
            return new CropInvitationEntity(
                invitation.Idcropinvitation,
                invitation.Accescode, // Maps DB 'Accescode' (typo)
                invitation.Createdat,
                invitation.Expiresat,
                invitation.Statusid,
                invitation.Createdby,
                invitation.Usedby,
                invitation.Cropid ?? 0 // Defaults CropId to 0 if null in DB.
            );
        }

        /// <summary>
        /// Maps a domain <see cref="CropInvitationEntity"/> to a database context <see cref="Cropinvitation"/> entity.
        /// Updates existing instance if provided, otherwise creates a new one.
        /// </summary>
        /// <param name="entity">The domain entity instance.</param>
        /// <param name="existingInvitation">Optional. The existing database entity to update.</param>
        /// <returns>The mapped or updated database entity instance.</returns>
        /// <remarks>Maps domain 'AccessCode' to DB 'Accescode' (typo).</remarks>
        private static Cropinvitation MapToDbModel(CropInvitationEntity entity, Cropinvitation? existingInvitation = null)
        {
            var invitation = existingInvitation ?? new Cropinvitation();
            invitation.Idcropinvitation = entity.IdCropInvitation;
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
            if (!_statusesInitialized || _pendingStatusId == 0)
                return Result<CropInvitationEntity>.Failure("Repository not properly initialized. Cannot verify invitation status.");

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
                if (invitation == null)
                    return Result<CropInvitationEntity>.Failure("Invitation code is invalid, expired, already used, or not found.");

                // Maps the found DB entity to domain entity and returns success.
                return Result<CropInvitationEntity>.Success(MapToDomainEntity(invitation));
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error retrieving active invitation by code: {AccessCode}", accessCode);
                return Result<CropInvitationEntity>.Failure("Error retrieving invitation by code.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<CropInvitationEntity>> GetById(int id)
        {
            // Status IDs not strictly needed for GetById, but calling doesn't hurt if already initialized.
            // await InitializeStatusIdsAsync();
            try
            {
                // Finds invitation by PK using AsNoTracking.
                var invitation = await _context.Cropinvitation.AsNoTracking().FirstOrDefaultAsync(i => i.Idcropinvitation == id);
                // Returns failure if not found.
                if (invitation == null) return Result<CropInvitationEntity>.Failure("Invitation not found.");
                // Maps and returns success.
                return Result<CropInvitationEntity>.Success(MapToDomainEntity(invitation));
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error retrieving invitation by ID: {InvitationId}", id);
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
                // Maps the list and returns success.
                return Result<IEnumerable<CropInvitationEntity>>.Success(invitations.Select(MapToDomainEntity));
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error retrieving all invitations");
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
                return Result<CropInvitationEntity>.Failure("Repository not properly initialized. Cannot set default invitation status.");

            try
            {
                // Maps domain entity to DB model.
                var dbModel = MapToDbModel(entity);
                // Sets default CreatedAt timestamp if not provided.
                if (dbModel.Createdat == default) dbModel.Createdat = _datetime.GetUtcNow();
                // Sets default StatusId to 'pending' if not provided.
                if (dbModel.Statusid == null) dbModel.Statusid = _pendingStatusId;

                // Adds the new model to the context.
                await _context.Cropinvitation.AddAsync(dbModel);
                // Saves changes to generate the ID.
                await _context.SaveChangesAsync();

                // --- Workaround: Update domain entity ID post-save ---
                var idProperty = typeof(CropInvitationEntity).GetProperty(nameof(CropInvitationEntity.IdCropInvitation));
                if (idProperty?.CanWrite ?? false)
                {
                    idProperty.SetValue(entity, dbModel.Idcropinvitation, null);
                }
                else
                {
                    _logger.Warning("Could not set IdCropInvitation on domain entity after creation.");
                    // Alternative: Return a newly mapped entity instead.
                    // return Result<CropInvitationEntity>.Success(MapToDomainEntity(dbModel));
                }
                // --- End Workaround ---

                // Returns success with the potentially updated domain entity.
                return Result<CropInvitationEntity>.Success(entity);
            }
            catch (DbUpdateException dbEx)
            {
                // Logs DB errors with structured entity data for context.
                _logger.Error(dbEx, "DB error creating invitation. Entity: {@InvitationEntity}", entity);
                return Result<CropInvitationEntity>.Failure("DB error creating invitation.");
            }
            catch (Exception ex)
            {
                // Logs general errors with structured entity data.
                _logger.Error(ex, "Error creating invitation. Entity: {@InvitationEntity}", entity);
                return Result<CropInvitationEntity>.Failure("Error creating invitation.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<bool>> Update(CropInvitationEntity entity)
        {
            // Basic entity null check.
            if (entity == null) return Result<bool>.Failure("Invitation entity cannot be null.");
            try
            {
                // Finds the existing entity by ID using FindAsync (tracked).
                var existing = await _context.Cropinvitation.FindAsync(entity.IdCropInvitation);
                // Returns failure if not found.
                if (existing == null) return Result<bool>.Failure("Invitation not found for update.");

                // Maps properties from domain entity onto the tracked database entity.
                MapToDbModel(entity, existing);

                // Marks the entity as modified explicitly.
                _context.Cropinvitation.Update(existing);
                // Saves changes.
                int rows = await _context.SaveChangesAsync();

                // Logs a warning if no rows were affected (potentially unchanged data or concurrency).
                if (rows == 0)
                    _logger.Warning("No changes saved for invitation ID: {InvitationId}. Entity: {@InvitationEntity}", entity.IdCropInvitation, entity);

                // Returns success if rows were affected.
                return Result<bool>.Success(rows > 0);
            }
            catch (DbUpdateConcurrencyException ex) // Specific handling for concurrency issues.
            {
                _logger.Warning(ex, "Concurrency error updating invitation ID {InvitationId}", entity.IdCropInvitation);
                return Result<bool>.Failure("Concurrency error updating invitation.");
            }
            catch (DbUpdateException dbEx) // Specific handling for other DB update issues.
            {
                _logger.Error(dbEx, "DB error updating invitation ID: {InvitationId}. Entity: {@InvitationEntity}", entity.IdCropInvitation, entity);
                return Result<bool>.Failure("DB error updating invitation.");
            }
            catch (Exception ex) // General error handler.
            {
                _logger.Error(ex, "Error updating invitation ID: {InvitationId}. Entity: {@InvitationEntity}", entity.IdCropInvitation, entity);
                return Result<bool>.Failure("Error updating invitation.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<bool>> Delete(int id)
        {
            try
            {
                // Finds the entity by ID using FindAsync (tracked).
                var invitation = await _context.Cropinvitation.FindAsync(id);
                // Returns failure if not found.
                if (invitation == null) return Result<bool>.Failure("Invitation not found for deletion.");

                // Removes the entity from the context.
                _context.Cropinvitation.Remove(invitation);
                // Saves changes.
                int rows = await _context.SaveChangesAsync();

                // Logs a warning if no rows were affected.
                if (rows == 0)
                    _logger.Warning("Failed to delete invitation ID (no rows affected): {InvitationId}", id);

                // Returns success if rows were affected.
                return Result<bool>.Success(rows > 0);
            }
            catch (DbUpdateException dbEx) // Handles DB deletion errors.
            {
                _logger.Error(dbEx, "DB error deleting invitation ID: {InvitationId}", id);
                return Result<bool>.Failure("DB error deleting invitation.");
            }
            catch (Exception ex) // Handles general errors.
            {
                _logger.Error(ex, "Error deleting invitation ID: {InvitationId}", id);
                return Result<bool>.Failure("Error deleting invitation.");
            }
        }
    }
}
