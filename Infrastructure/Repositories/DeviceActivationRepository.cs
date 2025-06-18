using ArandanoIRT_Backend.Domain.Entities;
using ArandanoIRT_Backend.Domain.IRepositories;
using ArandanoIRT_Backend.Domain.ValueObjects; 
using ArandanoIRT_Backend.Infrastructure.Data; 
using Microsoft.EntityFrameworkCore;

namespace ArandanoIRT_Backend.Infrastructure.Repositories
{
    /// <summary>
    /// Implements the <see cref="IDeviceActivationRepository"/> interface, providing data access logic
    /// for device activation entities (<see cref="DeviceActivationEntity"/>) using Entity Framework Core.
    /// </summary>
    public class DeviceActivationRepository : IDeviceActivationRepository
    {
        private readonly ApplicationDbContext _context; 
        private readonly ILogger<DeviceActivationRepository> _logger;

        // Static fields for storing activation status IDs once initialized.
        private static int _pendingStatusId;
        private static int _activatedStatusId; 
        private static int _expiredStatusId;
        private static int _revokedStatusId; 

        /// <summary>Flag indicating if activation status IDs have been successfully initialized.</summary>
        private static volatile bool _statusesInitialized; 

        /// <summary>Semaphore for thread-safe lazy initialization of activation status IDs.</summary>
        private static readonly SemaphoreSlim _initLock = new(1, 1);

        // Constants for activation status names used for initialization lookup.
        public const string PENDING_STATUS_NAME = "pendiente";
        public const string ACTIVATED_STATUS_NAME = "activo";
        public const string EXPIRED_STATUS_NAME = "expirado";
        public const string REVOKED_STATUS_NAME = "revocado"; 
        public const string TABLE_NAME = "deviceactivation";


        /// <summary>
        /// Initializes a new instance of the <see cref="DeviceActivationRepository"/> class.
        /// </summary>
        /// <param name="context">The database context.</param>
        /// <param name="logger">The logger instance.</param>
        /// <exception cref="ArgumentNullException">Thrown if context or logger is null.</exception>
        public DeviceActivationRepository(ApplicationDbContext context, ILogger<DeviceActivationRepository> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Asynchronously initialize status IDs in the background on repository creation
            // This helps warm up the repository but doesn't block the constructor.
            // Operations that require the IDs will explicitly await InitializeStatusIdsAsync.
            _ = InitializeStatusIdsAsync(); 
        }

        /// <summary>
        /// Initializes the static status ID fields by querying the database.
        /// Ensures this initialization is thread-safe and happens only once.
        /// </summary>
        /// <returns>A Task representing the asynchronous initialization operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown if status IDs cannot be found in the database.</exception>
        private async Task InitializeStatusIdsAsync()
        {
            // Use the semaphore to ensure only one thread initializes at a time.
            await _initLock.WaitAsync();
            try
            {
                // Double-check the flag inside the lock in case another thread finished
                // initialization while this thread was waiting for the lock.
                if (!_statusesInitialized)
                {
                    _logger.LogInformation("Initializing DeviceActivation status IDs from database.");

                    // Find the TableRelation ID for 'DeviceActivation'
                    var tableRelation = await _context.Tablerelation
                        .AsNoTracking()
                        .FirstOrDefaultAsync(tr => tr.Tablename == TABLE_NAME);

                    if (tableRelation == null)
                    {
                        _logger.LogError("TableRelation entry not found for table name: {TableName}", TABLE_NAME);
                        throw new InvalidOperationException($"TableRelation entry not found for table name: {TABLE_NAME}");
                    }

                    int tableRelationId = tableRelation.Idtablerelation;

                    // Retrieve status IDs from the Status table based on TableRelationId and status names
                    var statuses = await _context.Status
                        .AsNoTracking()
                        .Where(s => s.Tablerelationid == tableRelationId &&
                                    (s.Namestatus == PENDING_STATUS_NAME ||
                                     s.Namestatus == ACTIVATED_STATUS_NAME ||
                                     s.Namestatus == EXPIRED_STATUS_NAME ||
                                     s.Namestatus == REVOKED_STATUS_NAME))
                        .ToListAsync();

                    // Assign the retrieved IDs to the static fields.
                    _pendingStatusId = statuses.FirstOrDefault(s => s.Namestatus == PENDING_STATUS_NAME)?.Idstatus
                        ?? throw new InvalidOperationException($"Status ID not found for '{PENDING_STATUS_NAME}' in table '{TABLE_NAME}'.");

                    _activatedStatusId = statuses.FirstOrDefault(s => s.Namestatus == ACTIVATED_STATUS_NAME)?.Idstatus
                         ?? throw new InvalidOperationException($"Status ID not found for '{ACTIVATED_STATUS_NAME}' in table '{TABLE_NAME}'.");

                    _expiredStatusId = statuses.FirstOrDefault(s => s.Namestatus == EXPIRED_STATUS_NAME)?.Idstatus
                         ?? throw new InvalidOperationException($"Status ID not found for '{EXPIRED_STATUS_NAME}' in table '{TABLE_NAME}'.");

                    _revokedStatusId = statuses.FirstOrDefault(s => s.Namestatus == REVOKED_STATUS_NAME)?.Idstatus
                        ?? throw new InvalidOperationException($"Status ID not found for '{REVOKED_STATUS_NAME}' in table '{TABLE_NAME}'.");


                    // Set the flag to true to indicate successful initialization.
                    _statusesInitialized = true;
                    _logger.LogInformation("DeviceActivation status IDs successfully initialized.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize DeviceActivation status IDs.");
                // Critical failure: re-throw wrapped in a specific exception type if needed,
                // or let it propagate. Throwing InvalidOperationException signals a config/setup issue.
                throw new InvalidOperationException("Failed to initialize device activation status IDs from the database.", ex);
            }
            finally
            {
                // Release the semaphore.
                _initLock.Release();
            }
        }


        /// <summary>
        /// Maps an Entity Framework Core Deviceactivation model to a Domain DeviceActivationEntity.
        /// </summary>
        /// <param name="dbModel">The EF Core model.</param>
        /// <returns>The corresponding Domain Entity.</returns>
        private static DeviceActivationEntity? MapToDomainEntity(Deviceactivation? dbModel) 
        {
            if (dbModel == null) return null;

            // Use constructor. ActivatedAt has a public setter, ActivationStatus has private setter.
            var entity = new DeviceActivationEntity(
                dbModel.Iddeviceactivation,
                dbModel.Deviceid,
                dbModel.Activationcode,
                dbModel.Createdat,
                dbModel.Expiresat,
                dbModel.Activationstatus 
            );

            typeof(DeviceActivationEntity).GetProperty(nameof(DeviceActivationEntity.ActivatedAt))?.SetValue(entity, dbModel.Activatedat, null);


            return entity;
        }

        /// <summary>
        /// Maps a Domain DeviceActivationEntity to an Entity Framework Core Deviceactivation model.
        /// </summary>
        /// <param name="entity">The Domain Entity.</param>
        /// <param name="existingDbModel">Optional existing EF Core model to update.</param>
        /// <returns>The corresponding EF Core model.</returns>
        private static Deviceactivation MapToDbModel(DeviceActivationEntity entity, Deviceactivation? existingDbModel = null)
        {
            var dbModel = existingDbModel ?? new Deviceactivation();

            // Map properties from domain entity to DB model..
            if (existingDbModel != null)
            {
                dbModel.Iddeviceactivation = entity.IdDeviceActivation; // Keep existing ID for update context
            }

            dbModel.Deviceid = entity.DeviceId;
            dbModel.Activationcode = entity.ActivationCode;
            dbModel.Createdat = entity.CreatedAt;
            dbModel.Expiresat = entity.ExpiresAt;
            dbModel.Activatedat = entity.ActivatedAt; 
            dbModel.Activationstatus = entity.ActivationStatus; 

            return dbModel;
        }

        /// <inheritdoc/>
        public async Task<Result<DeviceActivationEntity>> Create(DeviceActivationEntity entity)
        {
            if (entity == null)
            {
                _logger.LogWarning("Attempted to create a null DeviceActivation entity.");
                return Result<DeviceActivationEntity>.Failure("Device activation entity cannot be null.");
            }

            try
            {
                // Ensure status IDs are initialized before using _pendingStatusId
                if (!_statusesInitialized)
                {
                    await InitializeStatusIdsAsync();
                }

                // Map the domain entity to the database model.
                var dbModel = MapToDbModel(entity);

                if (!dbModel.Activationstatus.HasValue)
                {
                    dbModel.Activationstatus = _pendingStatusId;
                    _logger.LogDebug("Setting initial activation status to Pending ({StatusId}) for new activation code.", _pendingStatusId);
                }
                else if (dbModel.Activationstatus != _pendingStatusId)
                {
                    // Log a warning if a create entity wasn't initialized with pending status
                    _logger.LogWarning("DeviceActivation entity provided for creation had status {StatusId} instead of Pending ({PendingStatusId}). Overriding to Pending.", dbModel.Activationstatus, _pendingStatusId);
                    dbModel.Activationstatus = _pendingStatusId;
                }


                await _context.Deviceactivation.AddAsync(dbModel);
                int rowsAffected = await _context.SaveChangesAsync();

                if (rowsAffected > 0)
                {
                    var createdEntity = MapToDomainEntity(dbModel);
                    if (createdEntity == null || createdEntity.IdDeviceActivation <= 0) // Defensive check
                    {
                        _logger.LogError("Failed to map newly created Deviceactivation DB model back to domain entity or ID is missing. DB ID: {DbActivationId}", dbModel.Iddeviceactivation);
                        return Result<DeviceActivationEntity>.Failure("Failed to map created device activation.");
                    }

                    _logger.LogInformation("Successfully created DeviceActivation with ID {DeviceActivationId} for Device ID {DeviceId}.", createdEntity.IdDeviceActivation, createdEntity.DeviceId);
                    return Result<DeviceActivationEntity>.Success(createdEntity);
                }
                else
                {
                    _logger.LogWarning("Failed to save new DeviceActivation to the database (no rows affected). Entity: {@ActivationEntity}", entity);
                    return Result<DeviceActivationEntity>.Failure("Failed to save device activation data.");
                }
            }
            catch (InvalidOperationException ioEx) // Catch exceptions from status initialization
            {
                _logger.LogError(ioEx, "Initialization error during DeviceActivation Create.");
                return Result<DeviceActivationEntity>.Failure("Service initialization error.");
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error creating DeviceActivation for Device ID {DeviceId}: {Message}", entity.DeviceId, dbEx.InnerException?.Message ?? dbEx.Message);
                return Result<DeviceActivationEntity>.Failure("Database error occurred while creating the device activation.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while creating DeviceActivation for Device ID {DeviceId}.", entity.DeviceId);
                return Result<DeviceActivationEntity>.Failure("An error occurred while creating the device activation.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<bool>> Delete(int id)
        {
            if (id <= 0)
            {
                _logger.LogWarning("Attempted to delete DeviceActivation with invalid ID: {DeviceActivationId}", id);
                return Result<bool>.Failure("Invalid device activation ID provided for deletion.");
            }

            try
            {
                var dbModelToDelete = await _context.Deviceactivation.FindAsync(id);

                if (dbModelToDelete == null)
                {
                    _logger.LogInformation("DeviceActivation with ID {DeviceActivationId} not found for deletion.", id);
                    return Result<bool>.Failure("Device activation not found for deletion.");
                }

                _context.Deviceactivation.Remove(dbModelToDelete);
                int rowsAffected = await _context.SaveChangesAsync();

                if (rowsAffected > 0)
                {
                    _logger.LogInformation("Successfully deleted DeviceActivation ID {DeviceActivationId}.", id);
                    return Result<bool>.Success(true);
                }
                else
                {
                    _logger.LogWarning("Failed to delete DeviceActivation ID {DeviceActivationId} (no rows affected).", id);
                    return Result<bool>.Failure("Failed to delete the device activation.");
                }
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error deleting DeviceActivation ID {DeviceActivationId}: {Message}", id, dbEx.InnerException?.Message ?? dbEx.Message);
                return Result<bool>.Failure("Database error occurred while deleting the device activation.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while deleting DeviceActivation ID {DeviceActivationId}.", id);
                return Result<bool>.Failure("An error occurred while deleting the device activation.");
            }
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Not implemented in this repository. This method is a placeholder and does not perform any operations.
        /// </remarks>
        public Task<Result<IEnumerable<DeviceActivationEntity>>> GetAll()
        {
            _logger.LogInformation("GetAll method called for DeviceActivation, but not implemented.");
            return Task.FromResult(Result <IEnumerable<DeviceActivationEntity>>.Failure("Operation not implemented for device activation."));
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Not implemented in this repository. This method is a placeholder and does not perform any operations.
        /// </remarks>
        public Task<Result<DeviceActivationEntity>> GetById(int id)
        {
            _logger.LogInformation("GetById method called for DeviceActivation ID {Id}, but not implemented.", id);
            return Task.FromResult(Result<DeviceActivationEntity>.Failure("Operation not implemented for device activation."));
        }

        /// <inheritdoc/>
        public async Task<Result<bool>> Update(DeviceActivationEntity entity)
        {
            if (entity == null)
            {
                _logger.LogWarning("Attempted to update a null DeviceActivation entity.");
                return Result<bool>.Failure("Device activation entity cannot be null for update.");
            }
            if (entity.IdDeviceActivation <= 0)
            {
                _logger.LogWarning("Attempted to update DeviceActivation with invalid ID: {DeviceActivationId}", entity.IdDeviceActivation);
                return Result<bool>.Failure("Invalid device activation ID provided for update.");
            }

            try
            {
                var existingDbModel = await _context.Deviceactivation.FindAsync(entity.IdDeviceActivation);

                if (existingDbModel == null)
                {
                    _logger.LogInformation("DeviceActivation with ID {DeviceActivationId} not found for update.", entity.IdDeviceActivation);
                    return Result<bool>.Failure("Device activation not found for update.");
                }

                // Map updated values onto the existing tracked model.
                MapToDbModel(entity, existingDbModel);

                int rowsAffected = await _context.SaveChangesAsync();

                if (rowsAffected > 0)
                {
                    _logger.LogInformation("Successfully updated DeviceActivation with ID {DeviceActivationId}.", entity.IdDeviceActivation);
                    return Result<bool>.Success(true);
                }
                else
                {
                    _logger.LogInformation("DeviceActivation with ID {DeviceActivationId} found, but no changes were saved during update.", entity.IdDeviceActivation);
                    return Result<bool>.Success(false); // Indicate no changes saved
                }
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex, "Concurrency conflict updating DeviceActivation ID {DeviceActivationId}.", entity.IdDeviceActivation);
                return Result<bool>.Failure("Concurrency conflict updating device activation. Please try again.");
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error updating DeviceActivation ID {DeviceActivationId}: {Message}", entity.IdDeviceActivation, dbEx.InnerException?.Message ?? dbEx.Message);
                return Result<bool>.Failure("Database error occurred while updating the device activation.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while updating DeviceActivation ID {DeviceActivationId}.", entity.IdDeviceActivation);
                return Result<bool>.Failure("An error occurred while updating the device activation.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<DeviceActivationEntity>> GetByActivationCodeAsync(string activationCode)
        {
            if (string.IsNullOrWhiteSpace(activationCode))
            {
                _logger.LogWarning("Attempted to retrieve DeviceActivation with empty or null activation code string.");
                return Result<DeviceActivationEntity>.Failure("Activation code string cannot be empty.");
            }

            try
            {
                // Ensure status IDs are initialized before querying by status
                if (!_statusesInitialized)
                {
                    await InitializeStatusIdsAsync();
                }

                // Find the activation record by code, checking if it's pending and not expired.
                // Use AsNoTracking for read operation.
                var dbModel = await _context.Deviceactivation
                                            .AsNoTracking()
                                            .FirstOrDefaultAsync(a => a.Activationcode == activationCode &&
                                                                      a.Activationstatus == _pendingStatusId && // Must be in pending status
                                                                      a.Expiresat > DateTime.UtcNow); // Must not be expired (compare with UTC)

                if (dbModel == null)
                {
                    _logger.LogInformation("DeviceActivation with code not found, not pending, or expired.");
                    return Result<DeviceActivationEntity>.Failure("Activation code is invalid or has expired.");
                }

                var entity = MapToDomainEntity(dbModel);
                if (entity == null) // Defensive check
                {
                    _logger.LogError("Failed to map Deviceactivation DB model by code to domain entity. DB ID: {DbActivationId}", dbModel.Iddeviceactivation);
                    return Result<DeviceActivationEntity>.Failure("Error retrieving device activation details.");
                }


                _logger.LogInformation("Successfully retrieved valid DeviceActivation by code (ID: {DeviceActivationId}).", entity.IdDeviceActivation);
                return Result<DeviceActivationEntity>.Success(entity);
            }
            catch (InvalidOperationException ioEx) // Catch exceptions from status initialization
            {
                _logger.LogError(ioEx, "Initialization error during GetByActivationCodeAsync.");
                return Result<DeviceActivationEntity>.Failure("Service initialization error.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while retrieving DeviceActivation by activation code.");
                return Result<DeviceActivationEntity>.Failure("An error occurred while retrieving the activation code.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result> UpdateStatusAsync(int deviceActivationId, int newStatusId, DateTime activatedAt)
        {
            if (deviceActivationId <= 0)
            {
                _logger.LogWarning("Attempted to update status for DeviceActivation with invalid ID: {DeviceActivationId}", deviceActivationId);
                return Result.Failure("Invalid device activation ID provided for status update.");
            }

            if (!_statusesInitialized)
            {
                try
                {
                    await InitializeStatusIdsAsync();
                }
                catch (InvalidOperationException initEx)
                {
                    _logger.LogError(initEx, "Initialization error during DeviceActivation UpdateStatusAsync for ID {DeviceActivationId}.", deviceActivationId);
                    return Result.Failure("Service configuration error during status update.");
                }
            }

            try
            {
                var dbModel = await _context.Deviceactivation.FindAsync(deviceActivationId);

                if (dbModel == null)
                {
                    _logger.LogWarning("DeviceActivation record with ID {DeviceActivationId} not found for status update.", deviceActivationId);
                    return Result.Failure("Device activation record not found.");
                }

                dbModel.Activationstatus = newStatusId;
                dbModel.Activatedat = activatedAt;

                int rowsAffected = await _context.SaveChangesAsync();

                if (rowsAffected > 0)
                {
                    _logger.LogInformation("Successfully updated status for DeviceActivation ID {DeviceActivationId} to Status ID {NewStatusId}.", deviceActivationId, newStatusId);
                    return Result.Success();
                }
                else
                {
                    _logger.LogWarning("Status update for DeviceActivation ID {DeviceActivationId} to Status ID {NewStatusId} did not affect any rows. The data might have been the same or a concurrency issue occurred without throwing DbUpdateConcurrencyException.", deviceActivationId, newStatusId);
                    return Result.Failure("Failed to update device activation status; no changes were saved.");
                }
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex, "Concurrency conflict updating status for DeviceActivation ID {DeviceActivationId}.", deviceActivationId);
                return Result.Failure("A concurrency conflict occurred while updating the device activation status. Please try again.");
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error updating status for DeviceActivation ID {DeviceActivationId}. InnerException: {InnerExceptionMessage}", deviceActivationId, dbEx.InnerException?.Message ?? dbEx.Message);
                return Result.Failure("A database error occurred while updating the device activation status.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while updating status for DeviceActivation ID {DeviceActivationId}.", deviceActivationId);
                return Result.Failure("An unexpected error occurred while updating the device activation status.");
            }
        }
    }
}