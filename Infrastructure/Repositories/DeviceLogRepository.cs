using ArandanoIRT_Backend.Domain.Entities;
using ArandanoIRT_Backend.Domain.IRepositories;
using ArandanoIRT_Backend.Domain.ValueObjects; 
using ArandanoIRT_Backend.Infrastructure.Data; 
using Microsoft.EntityFrameworkCore;

namespace ArandanoIRT_Backend.Infrastructure.Repositories
{
    /// <summary>
    /// Implements the <see cref="IDeviceLogRepository"/> interface, providing data access logic
    /// for device log entities (<see cref="DeviceLogEntity"/>) using Entity Framework Core.
    /// </summary>
    public class DeviceLogRepository : IDeviceLogRepository
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<DeviceLogRepository> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DeviceLogRepository"/> class.
        /// </summary>
        /// <param name="context">The database context.</param>
        /// <param name="logger">The logger instance.</param>
        /// <exception cref="ArgumentNullException">Thrown if context or logger is null.</exception>
        public DeviceLogRepository(ApplicationDbContext context, ILogger<DeviceLogRepository> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Maps an Entity Framework Core Devicelog model to a Domain DeviceLogEntity.
        /// </summary>
        /// <param name="dbModel">The EF Core model.</param>
        /// <returns>The corresponding Domain Entity.</returns>
        private static DeviceLogEntity? MapToDomainEntity(Devicelog? dbModel) 
        {
            if (dbModel == null) return null;

            return new DeviceLogEntity(
                dbModel.Iddevicelog,
                dbModel.Deviceid,
                dbModel.Logtype,
                dbModel.Logmessage,
                dbModel.Logtimestamp
            );
        }

        /// <summary>
        /// Maps a Domain DeviceLogEntity to an Entity Framework Core Devicelog model.
        /// </summary>
        /// <param name="entity">The Domain Entity.</param>
        /// <param name="existingDbModel">Optional existing EF Core model to update.</param>
        /// <returns>The corresponding EF Core model.</returns>
        private static Devicelog MapToDbModel(DeviceLogEntity entity, Devicelog? existingDbModel = null)
        {
            var dbModel = existingDbModel ?? new Devicelog();

            // Map properties from domain entity to DB model.
            if (existingDbModel != null)
            {
                dbModel.Iddevicelog = entity.IdDeviceLog; // Keep existing ID for update context
            }

            dbModel.Deviceid = entity.DeviceId;
            dbModel.Logtype = entity.LogType;
            dbModel.Logmessage = entity.LogMessage;
            dbModel.Logtimestamp = entity.LogTimestamp; // Map timestamp from entity

            return dbModel;
        }

        /// <inheritdoc/>
        public async Task<Result<DeviceLogEntity>> Create(DeviceLogEntity entity)
        {
            if (entity == null)
            {
                _logger.LogWarning("Attempted to create a null DeviceLog entity.");
                return Result<DeviceLogEntity>.Failure("Device log entity cannot be null.");
            }

            try
            {
                // Map the domain entity to the database model.
                var dbModel = MapToDbModel(entity);

                // Add to the context. EF will handle the database ID generation.
                await _context.Devicelog.AddAsync(dbModel);

                // Save changes to the database.
                int rowsAffected = await _context.SaveChangesAsync();

                if (rowsAffected > 0)
                {
                    // After SaveChangesAsync, dbModel will have the database-generated ID.
                    // Map the saved DB model (with ID) back to a new domain entity to return.
                    var createdEntity = MapToDomainEntity(dbModel);

                    if (createdEntity == null || createdEntity.IdDeviceLog <= 0) // Defensive check
                    {
                        _logger.LogError("Failed to map newly created Devicelog DB model back to domain entity or ID is missing. DB ID: {DbLogId}", dbModel.Iddevicelog);
                        return Result<DeviceLogEntity>.Failure("Failed to map created device log.");
                    }


                    _logger.LogInformation("Successfully created DeviceLog with ID {DeviceLogId} for Device ID {DeviceId}.", createdEntity.IdDeviceLog, createdEntity.DeviceId);
                    return Result<DeviceLogEntity>.Success(createdEntity);
                }
                else
                {
                    _logger.LogWarning("Failed to save new DeviceLog to the database (no rows affected). Entity: {@LogEntity}", entity);
                    return Result<DeviceLogEntity>.Failure("Failed to save device log to the database.");
                }
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error creating DeviceLog for Device ID {DeviceId}: {Message}", entity.DeviceId, dbEx.InnerException?.Message ?? dbEx.Message);
                return Result<DeviceLogEntity>.Failure("Database error occurred while creating the device log.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while creating DeviceLog for Device ID {DeviceId}.", entity.DeviceId);
                return Result<DeviceLogEntity>.Failure("An error occurred while creating the device log.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<IEnumerable<DeviceLogEntity>>> GetLogsByDeviceIdAsync(int deviceId)
        {
            if (deviceId <= 0)
            {
                _logger.LogWarning("Attempted to retrieve logs with invalid Device ID: {DeviceId}", deviceId);
                return Result<IEnumerable<DeviceLogEntity>>.Failure("Invalid device ID provided for retrieving logs.");
            }

            try
            {
                // Retrieve logs for the specific device, ordered by timestamp descending.
                // Use AsNoTracking for read operations.
                var dbModels = await _context.Devicelog
                                             .AsNoTracking() // Read-only operation
                                             .Where(log => log.Deviceid == deviceId)
                                             .OrderByDescending(log => log.Logtimestamp)
                                             .ToListAsync();

                // Map the list of database models to domain entities.
                var entities = dbModels.Select(MapToDomainEntity).OfType<DeviceLogEntity>().ToList();


                _logger.LogInformation("Successfully retrieved {Count} DeviceLog records for Device ID {DeviceId}.", entities.Count, deviceId);
                return Result<IEnumerable<DeviceLogEntity>>.Success(entities);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while retrieving DeviceLog records for Device ID {DeviceId}.", deviceId);
                return Result<IEnumerable<DeviceLogEntity>>.Failure("An error occurred while retrieving device logs.");
            }
        }


        // --- Basic/Placeholder implementations for other methods ---

        /// <inheritdoc/>
        /// <remarks>
        /// Not implemented for DeviceLogEntity. This is a placeholder method.
        /// </remarks>
        public Task<Result<DeviceLogEntity>> GetById(int id)
        {
            _logger.LogInformation("GetById method called for DeviceLog ID {Id}, but not implemented.", id);
            return Task.FromResult(Result<DeviceLogEntity>.Failure("Operation not implemented for device logs."));
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Not implemented for DeviceLogEntity. This is a placeholder method.
        /// </remarks>
        public Task<Result<IEnumerable<DeviceLogEntity>>> GetAll()
        {
            _logger.LogInformation("GetAll method called for DeviceLog, but not implemented.");
            return Task.FromResult(Result<IEnumerable<DeviceLogEntity>>.Failure("Operation not implemented for device logs."));

        }

        /// <inheritdoc/>
        /// <remarks>
        /// Not implemented for DeviceLogEntity. This is a placeholder method.
        /// </remarks>
        public Task<Result<bool>> Update(DeviceLogEntity entity)
        {
            _logger.LogInformation("Update method called for DeviceLog ID {Id}, but not implemented.", entity.IdDeviceLog);
            return Task.FromResult(Result<bool>.Failure("Operation not implemented for device logs."));
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Not implemented for DeviceLogEntity. This is a placeholder method.
        /// </remarks>
        public Task<Result<bool>> Delete(int id)
        {
            _logger.LogInformation("Delete method called for DeviceLog ID {Id}, but not implemented (handled by DeviceData deletion).", id);
            return Task.FromResult(Result<bool>.Failure("Operation not implemented for device logs."));
        }
    }
}