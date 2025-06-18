using ArandanoIRT_Backend.Domain.Entities;
using ArandanoIRT_Backend.Domain.IRepositories;
using ArandanoIRT_Backend.Domain.ValueObjects; 
using ArandanoIRT_Backend.Infrastructure.Data; 
using Microsoft.EntityFrameworkCore;

namespace ArandanoIRT_Backend.Infrastructure.Repositories
{
    /// <summary>
    /// Implements the <see cref="IDeviceDataRepository"/> interface, providing data access logic
    /// for device entities (<see cref="DeviceDataEntity"/>) using Entity Framework Core.
    /// </summary>
    public class DeviceDataRepository : IDeviceDataRepository
    {
        private readonly ApplicationDbContext _context; 
        private readonly ILogger<DeviceDataRepository> _logger;

        // Static fields for storing device status IDs once initialized.
        private static int _activeStatusId;
        private static int _inactiveStatusId;
        private static int _maintenanceStatusId;

        /// <summary>Flag indicating if device status IDs have been successfully initialized.</summary>
        private static volatile bool _statusesInitialized;

        /// <summary>Semaphore for thread-safe lazy initialization of device status IDs.</summary>
        private static readonly SemaphoreSlim _initLock = new(1, 1);

        // Constants for device status names used for initialization lookup.
        public const string ACTIVE_STATUS_NAME = "activo";
        public const string INACTIVE_STATUS_NAME = "inactivo";
        public const string MAINTENANCE_STATUS_NAME = "mantenimiento";
        public const string TABLE_NAME = "DeviceData";


        /// <summary>
        /// Initializes a new instance of the <see cref="DeviceDataRepository"/> class.
        /// </summary>
        /// <param name="context">The database context.</param>
        /// <param name="logger">The logger instance.</param>
        /// <exception cref="ArgumentNullException">Thrown if context or logger is null.</exception>
        public DeviceDataRepository(ApplicationDbContext context, ILogger<DeviceDataRepository> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Asynchronously initialize status IDs in the background
            _ = InitializeStatusIdsAsync(); // Fire and forget
        }

        /// <summary>
        /// Initializes the static status ID fields by querying the database.
        /// Ensures this initialization is thread-safe and happens only once.
        /// </summary>
        /// <returns>A Task representing the asynchronous initialization operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown if status IDs cannot be found in the database.</exception>
        private async Task InitializeStatusIdsAsync()
        {
            await _initLock.WaitAsync();
            try
            {
                if (!_statusesInitialized)
                {
                    _logger.LogInformation("Initializing DeviceData status IDs from database.");

                    var tableRelation = await _context.Tablerelation
                        .AsNoTracking()
                        .FirstOrDefaultAsync(tr => tr.Tablename == TABLE_NAME);

                    if (tableRelation == null)
                    {
                        _logger.LogError("TableRelation entry not found for table name: {TableName}", TABLE_NAME);
                        throw new InvalidOperationException($"TableRelation entry not found for table name: {TABLE_NAME}");
                    }

                    int tableRelationId = tableRelation.Idtablerelation;

                    var statuses = await _context.Status
                        .AsNoTracking()
                        .Where(s => s.Tablerelationid == tableRelationId &&
                                    (s.Namestatus == ACTIVE_STATUS_NAME ||
                                     s.Namestatus == INACTIVE_STATUS_NAME ||
                                     s.Namestatus == MAINTENANCE_STATUS_NAME))
                        .ToListAsync();

                    _activeStatusId = statuses.FirstOrDefault(s => s.Namestatus == ACTIVE_STATUS_NAME)?.Idstatus
                        ?? throw new InvalidOperationException($"Status ID not found for '{ACTIVE_STATUS_NAME}' in table '{TABLE_NAME}'.");

                    _inactiveStatusId = statuses.FirstOrDefault(s => s.Namestatus == INACTIVE_STATUS_NAME)?.Idstatus
                         ?? throw new InvalidOperationException($"Status ID not found for '{INACTIVE_STATUS_NAME}' in table '{TABLE_NAME}'.");

                    _maintenanceStatusId = statuses.FirstOrDefault(s => s.Namestatus == MAINTENANCE_STATUS_NAME)?.Idstatus
                         ?? throw new InvalidOperationException($"Status ID not found for '{MAINTENANCE_STATUS_NAME}' in table '{TABLE_NAME}'.");


                    _statusesInitialized = true;
                    _logger.LogInformation("DeviceData status IDs successfully initialized.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize DeviceData status IDs.");
                throw new InvalidOperationException("Failed to initialize device data status IDs from the database.", ex);
            }
            finally
            {
                _initLock.Release();
            }
        }


        /// <summary>
        /// Maps an Entity Framework Core Devicedata model to a Domain DeviceDataEntity.
        /// </summary>
        /// <param name="dbModel">The EF Core model.</param>
        /// <returns>The corresponding Domain Entity.</returns>
        private static DeviceDataEntity? MapToDomainEntity(Devicedata? dbModel) 
        {
            if (dbModel == null) return null;

            var nameDevice = dbModel.Namedevice ?? string.Empty;

            var entity = new DeviceDataEntity(
                dbModel.Iddevicedata,
                nameDevice, 
                dbModel.Descriptiondevice,
                dbModel.Datacollectiontime,
                dbModel.Registeredat,
                dbModel.Registeredby,
                dbModel.Statusid,
                dbModel.Cropid,
                dbModel.Plantid
            );

            // Use reflection to set private setters for properties not in the constructor,
            typeof(DeviceDataEntity).GetProperty(nameof(DeviceDataEntity.UpdatedAt))?.SetValue(entity, dbModel.Updatedat, null);
            typeof(DeviceDataEntity).GetProperty(nameof(DeviceDataEntity.UpdatedBy))?.SetValue(entity, dbModel.Updatedby, null);

            // Set related names if included in the query
            typeof(DeviceDataEntity).GetProperty(nameof(DeviceDataEntity.StatusName))?.SetValue(entity, dbModel.Status?.Namestatus, null);
            typeof(DeviceDataEntity).GetProperty(nameof(DeviceDataEntity.RegisteredByName))?.SetValue(entity, dbModel.RegisteredbyNavigation != null ? $"{dbModel.RegisteredbyNavigation.Firstname} {dbModel.RegisteredbyNavigation.Lastname}".Trim() : null, null);
            typeof(DeviceDataEntity).GetProperty(nameof(DeviceDataEntity.UpdatedByName))?.SetValue(entity, dbModel.UpdatedbyNavigation != null ? $"{dbModel.UpdatedbyNavigation.Firstname} {dbModel.UpdatedbyNavigation.Lastname}".Trim() : null, null);
            typeof(DeviceDataEntity).GetProperty(nameof(DeviceDataEntity.CropName))?.SetValue(entity, dbModel.Crop?.Namecrop, null);
            typeof(DeviceDataEntity).GetProperty(nameof(DeviceDataEntity.PlantName))?.SetValue(entity, dbModel.Plant?.Nameplant, null);

            return entity;
        }

        /// <summary>
        /// Maps a Domain DeviceDataEntity to an Entity Framework Core Devicedata model.
        /// </summary>
        /// <param name="entity">The Domain Entity.</param>
        /// <param name="existingDbModel">Optional existing EF Core model to update.</param>
        /// <returns>The corresponding EF Core model.</returns>
        private static Devicedata MapToDbModel(DeviceDataEntity entity, Devicedata? existingDbModel = null)
        {
            var dbModel = existingDbModel ?? new Devicedata();

            // Map properties from domain entity to DB model.
            if (existingDbModel != null)
            {
                dbModel.Iddevicedata = entity.IdDeviceData; // Keep existing ID for update context
            }

            dbModel.Namedevice = entity.NameDevice;
            dbModel.Descriptiondevice = entity.DescriptionDevice;
            dbModel.Datacollectiontime = entity.DataCollectionTime;
            dbModel.Statusid = entity.StatusId;
            dbModel.Registeredat = entity.RegisteredAt;
            dbModel.Registeredby = entity.RegisteredBy;
            dbModel.Updatedat = entity.UpdatedAt;
            dbModel.Updatedby = entity.UpdatedBy;
            dbModel.Cropid = entity.CropId;
            dbModel.Plantid = entity.PlantId;

            return dbModel;
        }


        /// <inheritdoc/>
        public async Task<Result<DeviceDataEntity>> Create(DeviceDataEntity entity)
        {
            if (entity == null)
            {
                _logger.LogWarning("Attempted to create a null DeviceData entity.");
                return Result<DeviceDataEntity>.Failure("Device data entity cannot be null.");
            }

            // Ensure status IDs are initialized before any operation that might use them
            if (!_statusesInitialized)
            {
                try
                {
                    await InitializeStatusIdsAsync();
                }
                catch (InvalidOperationException ioEx)
                {
                    _logger.LogError(ioEx, "Initialization error during DeviceData Create.");
                    return Result<DeviceDataEntity>.Failure("Service initialization error.");
                }
            }


            try
            {
                // Map the domain entity to the database model.
                var dbModel = MapToDbModel(entity);

                if (!dbModel.Statusid.HasValue)
                {
                    dbModel.Statusid = _inactiveStatusId; 
                    _logger.LogDebug("Setting default initial status to Inactive ({StatusId}) for new device.", _inactiveStatusId);
                }


                await _context.Devicedata.AddAsync(dbModel);

                int rowsAffected = await _context.SaveChangesAsync();

                if (rowsAffected > 0)
                {
                    var createdEntity = MapToDomainEntity(dbModel);

                    if (createdEntity == null || createdEntity.IdDeviceData <= 0)
                    {
                        _logger.LogError("Failed to map newly created Devicedata DB model back to domain entity or ID is missing. DB ID: {DbPersonId}", dbModel.Iddevicedata);
                        return Result<DeviceDataEntity>.Failure("Failed to map created device.");
                    }

                    _logger.LogInformation("Successfully created DeviceData with ID {DeviceDataId}.", createdEntity.IdDeviceData);
                    return Result<DeviceDataEntity>.Success(createdEntity);
                }
                else
                {
                    _logger.LogWarning("Failed to save new DeviceData to the database (no rows affected). Entity: {@DeviceEntity}", entity);
                    return Result<DeviceDataEntity>.Failure("Failed to save device data to the database.");
                }
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error creating DeviceData: {Message}", dbEx.InnerException?.Message ?? dbEx.Message);
                return Result<DeviceDataEntity>.Failure("Database error occurred while creating the device.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while creating DeviceData.");
                return Result<DeviceDataEntity>.Failure("An error occurred while creating the device.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<DeviceDataEntity>> GetById(int id)
        {
            if (id <= 0)
            {
                _logger.LogWarning("Attempted to retrieve DeviceData with invalid ID: {DeviceId}", id);
                return Result<DeviceDataEntity>.Failure("Invalid device ID provided.");
            }

            // Ensure status IDs are initialized before any operation that might use them (e.g., filtering by status)
            if (!_statusesInitialized)
            {
                try
                {
                    await InitializeStatusIdsAsync();
                }
                catch (InvalidOperationException ioEx)
                {
                    _logger.LogError(ioEx, "Initialization error during DeviceData GetById.");
                    return Result<DeviceDataEntity>.Failure("Service initialization error.");
                }
            }


            try
            {
                var dbModel = await _context.Devicedata
                                            .AsNoTracking()
                                            .Include(d => d.Status)
                                            .Include(d => d.Crop)
                                            .Include(d => d.Plant)
                                            .Include(d => d.UpdatedbyNavigation)
                                            .Include(d => d.RegisteredbyNavigation)
                                            .FirstOrDefaultAsync(d => d.Iddevicedata == id);

                if (dbModel == null)
                {
                    _logger.LogInformation("DeviceData with ID {DeviceId} not found.", id);
                    return Result<DeviceDataEntity>.Failure("Device not found.");
                }

                var entity = MapToDomainEntity(dbModel);

                if (entity == null)
                {
                    _logger.LogError("Failed to map Devicedata DB model with ID {DeviceId} to domain entity.", id);
                    return Result<DeviceDataEntity>.Failure("Error retrieving device details.");
                }


                _logger.LogInformation("Successfully retrieved DeviceData with ID {DeviceId}.", id);
                return Result<DeviceDataEntity>.Success(entity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while retrieving DeviceData by ID {DeviceId}.", id);
                return Result<DeviceDataEntity>.Failure("An error occurred while retrieving device details.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<IEnumerable<DeviceDataEntity>>> GetAll()
        {
            // Ensure status IDs are initialized before any operation that might use them (e.g., filtering by status)
            if (!_statusesInitialized)
            {
                try
                {
                    await InitializeStatusIdsAsync();
                }
                catch (InvalidOperationException ioEx)
                {
                    _logger.LogError(ioEx, "Initialization error during DeviceData GetAll.");
                    return Result <IEnumerable<DeviceDataEntity>>.Failure("Service initialization error.");
                }
            }

            try
            {
                var dbModels = await _context.Devicedata
                                             .AsNoTracking()
                                             .Include(d => d.Status)
                                             .Include(d => d.Crop)
                                             .Include(d => d.Plant)
                                             .ToListAsync();

                var entities = dbModels.Select(MapToDomainEntity).OfType<DeviceDataEntity>().ToList();


                _logger.LogInformation("Successfully retrieved {Count} DeviceData records.", entities.Count);
                return Result<IEnumerable<DeviceDataEntity>>.Success(entities);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while retrieving all DeviceData records.");
                return Result<IEnumerable<DeviceDataEntity>>.Failure("An error occurred while retrieving devices.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<bool>> Update(DeviceDataEntity entity)
        {
            if (entity == null)
            {
                _logger.LogWarning("Attempted to update a null DeviceData entity.");
                return Result<bool>.Failure("Device data entity cannot be null for update.");
            }
            if (entity.IdDeviceData <= 0)
            {
                _logger.LogWarning("Attempted to update DeviceData with invalid ID: {DeviceId}", entity.IdDeviceData);
                return Result<bool>.Failure("Invalid device ID provided for update.");
            }

            // Ensure status IDs are initialized before any operation that might use them (e.g., validating status)
            if (!_statusesInitialized)
            {
                try
                {
                    await InitializeStatusIdsAsync();
                }
                catch (InvalidOperationException ioEx)
                {
                    _logger.LogError(ioEx, "Initialization error during DeviceData Update.");
                    return Result<bool>.Failure("Service initialization error.");
                }
            }

            try
            {
                var existingDbModel = await _context.Devicedata.FindAsync(entity.IdDeviceData);

                if (existingDbModel == null)
                {
                    _logger.LogInformation("DeviceData with ID {DeviceId} not found for update.", entity.IdDeviceData);
                    return Result<bool>.Failure("Device not found for update.");
                }

                MapToDbModel(entity, existingDbModel);

                int rowsAffected = await _context.SaveChangesAsync();

                if (rowsAffected > 0)
                {
                    _logger.LogInformation("Successfully updated DeviceData with ID {DeviceId}.", entity.IdDeviceData);
                    return Result<bool>.Success(true);
                }
                else
                {
                    _logger.LogInformation("DeviceData with ID {DeviceId} found, but no changes were saved during update.", entity.IdDeviceData);
                    return Result<bool>.Success(false);
                }
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex, "Concurrency conflict updating DeviceData ID {DeviceId}.", entity.IdDeviceData);
                return Result<bool>.Failure("Concurrency conflict updating device data. Please try again.");
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error updating DeviceData ID {DeviceId}: {Message}", entity.IdDeviceData, dbEx.InnerException?.Message ?? dbEx.Message);
                return Result<bool>.Failure("Database error occurred while updating the device.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while updating DeviceData ID {DeviceId}.", entity.IdDeviceData);
                return Result<bool>.Failure("An error occurred while updating the device.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<bool>> Delete(int id)
        {
            if (id <= 0)
            {
                _logger.LogWarning("Attempted to delete DeviceData with invalid ID: {DeviceId}", id);
                return Result<bool>.Failure("Invalid device ID provided for deletion.");
            }

            // Ensure status IDs are initialized before any operation that might use them (less likely for delete, but good practice if related status logic is involved)
            if (!_statusesInitialized)
            {
                try
                {
                    await InitializeStatusIdsAsync();
                }
                catch (InvalidOperationException ioEx)
                {
                    _logger.LogError(ioEx, "Initialization error during DeviceData Delete.");
                    return Result<bool>.Failure("Service initialization error.");
                }
            }


            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var dbModelToDelete = await _context.Devicedata.FindAsync(id);

                if (dbModelToDelete == null)
                {
                    _logger.LogInformation("DeviceData with ID {DeviceId} not found for deletion.", id);
                    await transaction.RollbackAsync();
                    return Result<bool>.Failure("Device not found for deletion.");
                }

                _logger.LogInformation("Deleting related entities for DeviceData ID {DeviceId} within a transaction.", id);

                // 1. Delete related DeviceLogs
                var relatedLogs = await _context.Devicelog.Where(log => log.Deviceid == id).ToListAsync();
                if (relatedLogs.Count > 0)
                {
                    _context.Devicelog.RemoveRange(relatedLogs);
                    _logger.LogDebug("Marked {Count} related DeviceLog entities for deletion.", relatedLogs.Count);
                }


                // 2. Delete related DeviceTokens
                var relatedTokens = await _context.Devicetoken.Where(token => token.Deviceid == id).ToListAsync();
                if (relatedTokens.Count > 0)
                {
                    _context.Devicetoken.RemoveRange(relatedTokens);
                    _logger.LogDebug("Marked {Count} related DeviceToken entities for deletion.", relatedTokens.Count);
                }

                // 3. Delete related DeviceActivations
                var relatedActivations = await _context.Deviceactivation.Where(activation => activation.Deviceid == id).ToListAsync();
                if (relatedActivations.Count > 0)
                {
                    _context.Deviceactivation.RemoveRange(relatedActivations);
                    _logger.LogDebug("Marked {Count} related DeviceActivation entities for deletion.", relatedActivations.Count);
                }


                // 4. Delete the main DeviceData entity
                _context.Devicedata.Remove(dbModelToDelete);
                _logger.LogDebug("Marked DeviceData entity ID {DeviceId} for deletion.", id);


                int rowsAffected = await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                if (rowsAffected > 0)
                {
                    _logger.LogInformation("Successfully deleted DeviceData ID {DeviceId} and {RowsAffected} related records.", id, rowsAffected - 1);
                    return Result<bool>.Success(true);
                }
                else
                {
                    _logger.LogError("Failed to delete DeviceData ID {DeviceId} and related records (SaveChangesAsync reported 0 rows affected after removing entities).", id);
                    await transaction.RollbackAsync();
                    return Result<bool>.Failure("Failed to delete the device.");
                }

            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex, "Concurrency conflict deleting DeviceData ID {DeviceId} or its related entities.", id);
                await transaction.RollbackAsync();
                return Result<bool>.Failure("Concurrency conflict deleting device. Please try again.");
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error deleting DeviceData ID {DeviceId} or its related entities: {Message}", id, dbEx.InnerException?.Message ?? dbEx.Message);
                await transaction.RollbackAsync();
                return Result<bool>.Failure("Database error occurred while deleting the device.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while deleting DeviceData ID {DeviceId} or its related entities.", id);
                await transaction.RollbackAsync();
                return Result<bool>.Failure("An error occurred while deleting the device.");
            }
        }
    }
}