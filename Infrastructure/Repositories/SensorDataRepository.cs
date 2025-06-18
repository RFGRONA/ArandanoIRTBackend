using ArandanoIRT_Backend.Domain.Entities;
using ArandanoIRT_Backend.Domain.IRepositories;
using ArandanoIRT_Backend.Domain.ValueObjects; 
using ArandanoIRT_Backend.Infrastructure.Data; 
using Microsoft.EntityFrameworkCore;
namespace ArandanoIRT_Backend.Infrastructure.Repositories
{
    /// <summary>
    /// Implements the <see cref="ISensorDataRepository"/> interface, providing data access logic
    /// for sensor data entities (<see cref="SensorDataEntity"/>) using Entity Framework Core.
    /// </summary>
    public class SensorDataRepository : ISensorDataRepository
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SensorDataRepository> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="SensorDataRepository"/> class.
        /// </summary>
        /// <param name="context">The database context.</param>
        /// <param name="logger">The logger instance.</param>
        /// <exception cref="ArgumentNullException">Thrown if context or logger is null.</exception>
        public SensorDataRepository(ApplicationDbContext context, ILogger<SensorDataRepository> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Maps an Entity Framework Core Sensordata model to a Domain SensorDataEntity.
        /// </summary>
        /// <param name="dbModel">The EF Core model.</param>
        /// <returns>The corresponding Domain Entity.</returns>
        private static SensorDataEntity? MapToDomainEntity(Sensordata? dbModel)
        {
            if (dbModel == null) return null;

            // SensorDataEntity constructor takes all core properties.
            return new SensorDataEntity(
                dbModel.Idsensordata,
                dbModel.Temperature,
                dbModel.Humidity,
                dbModel.Lightintensity,
                dbModel.Citytemperature,
                dbModel.Cityhumidity,
                dbModel.Recordedat,
                dbModel.Plantid,
                dbModel.Cropid
            );
        }

        /// <summary>
        /// Maps a Domain SensorDataEntity to an Entity Framework Core Sensordata model.
        /// </summary>
        /// <param name="entity">The Domain Entity.</param>
        /// <param name="existingDbModel">Optional existing EF Core model to update (less common for sensor data).</param>
        /// <returns>The corresponding EF Core model.</returns>
        private static Sensordata MapToDbModel(SensorDataEntity entity, Sensordata? existingDbModel = null)
        {
            var dbModel = existingDbModel ?? new Sensordata();

            // Map properties from domain entity to DB model. ID is DB generated on Create.
            if (existingDbModel != null)
            {
                dbModel.Idsensordata = entity.IdSensorData; // Keep existing ID for update context
            }

            dbModel.Temperature = entity.Temperature;
            dbModel.Humidity = entity.Humidity;
            dbModel.Lightintensity = entity.LightIntensity;
            dbModel.Citytemperature = entity.CityTemperature;
            dbModel.Cityhumidity = entity.CityHumidity;
            dbModel.Recordedat = entity.RecordedAt;
            dbModel.Plantid = entity.PlantId;
            dbModel.Cropid = entity.CropId;

            return dbModel;
        }

        /// <inheritdoc/>
        public async Task<Result<SensorDataEntity>> Create(SensorDataEntity entity)
        {
            if (entity == null)
            {
                _logger.LogWarning("Attempted to create a null SensorData entity.");
                return Result<SensorDataEntity>.Failure("Sensor data entity cannot be null.");
            }

            try
            {
                var dbModel = MapToDbModel(entity);

                await _context.Sensordata.AddAsync(dbModel);
                int rowsAffected = await _context.SaveChangesAsync();

                if (rowsAffected > 0)
                {
                    var createdEntity = MapToDomainEntity(dbModel);
                    if (createdEntity == null || createdEntity.IdSensorData <= 0) // Defensive check
                    {
                        _logger.LogError("Failed to map newly created Sensordata DB model back to domain entity or ID is missing. DB ID: {DbDataId}", dbModel.Idsensordata);
                        return Result<SensorDataEntity>.Failure("Failed to map created sensor data.");
                    }

                    _logger.LogInformation("Successfully created SensorData with ID {SensorDataId} for Plant ID {PlantId}.", createdEntity.IdSensorData, createdEntity.PlantId);
                    return Result<SensorDataEntity>.Success(createdEntity);
                }
                else
                {
                    _logger.LogWarning("Failed to save new SensorData to the database (no rows affected). Entity: {@DataEntity}", entity);
                    return Result<SensorDataEntity>.Failure("Failed to save sensor data to the database.");
                }
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error creating SensorData for Plant ID {PlantId}: {Message}", entity.PlantId, dbEx.InnerException?.Message ?? dbEx.Message);
                return Result<SensorDataEntity>.Failure("Database error occurred while creating the sensor data.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while creating SensorData for Plant ID {PlantId}.", entity.PlantId);
                return Result<SensorDataEntity>.Failure("An error occurred while creating the sensor data.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<SensorDataEntity>> GetById(int id)
        {
            if (id <= 0)
            {
                _logger.LogWarning("Attempted to retrieve SensorData with invalid ID: {SensorDataId}", id);
                return Result<SensorDataEntity>.Failure("Invalid sensor data ID provided.");
            }

            try
            {
                var dbModel = await _context.Sensordata
                                            .AsNoTracking() // Read-only operation
                                            .FirstOrDefaultAsync(d => d.Idsensordata == id);

                if (dbModel == null)
                {
                    _logger.LogInformation("SensorData with ID {SensorDataId} not found.", id);
                    return Result<SensorDataEntity>.Failure("Sensor data not found.");
                }

                var entity = MapToDomainEntity(dbModel);
                if (entity == null) // Defensive check for mapping failure
                {
                    _logger.LogError("Failed to map Sensordata DB model with ID {SensorDataId} to domain entity.", id);
                    return Result<SensorDataEntity>.Failure("Error retrieving sensor data details.");
                }

                _logger.LogInformation("Successfully retrieved SensorData with ID {SensorDataId}.", id);
                return Result<SensorDataEntity>.Success(entity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while retrieving SensorData by ID {SensorDataId}.", id);
                return Result<SensorDataEntity>.Failure("An error occurred while retrieving sensor data details.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<IEnumerable<SensorDataEntity>>> GetAllByPlantIdAsync(int plantId)
        {
            if (plantId <= 0)
            {
                _logger.LogWarning("Attempted to retrieve SensorData with invalid Plant ID: {PlantId}", plantId);
                return Result<IEnumerable<SensorDataEntity>>.Failure("Invalid plant ID provided for retrieving sensor data.");
            }

            try
            {
                // Retrieve all sensor data for the specific plant, ordered by recording time descending.
                var dbModels = await _context.Sensordata
                                             .AsNoTracking() // Read-only operation
                                             .Where(d => d.Plantid == plantId)
                                             .OrderByDescending(d => d.Recordedat)
                                             .ToListAsync();

                // Map the list of database models to domain entities.
                var entities = dbModels.Select(MapToDomainEntity).OfType<SensorDataEntity>().ToList();


                _logger.LogInformation("Successfully retrieved {Count} SensorData records for Plant ID {PlantId}.", entities.Count, plantId);
                return Result<IEnumerable<SensorDataEntity>>.Success(entities);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while retrieving SensorData records for Plant ID {PlantId}.", plantId);
                return Result<IEnumerable<SensorDataEntity>>.Failure("An error occurred while retrieving sensor data for the plant.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<SensorDataEntity>> GetLatestByPlantIdAsync(int plantId)
        {
            if (plantId <= 0)
            {
                _logger.LogWarning("Attempted to retrieve latest SensorData with invalid Plant ID: {PlantId}", plantId);
                return Result<SensorDataEntity>.Failure("Invalid plant ID provided for retrieving latest sensor data.");
            }

            try
            {
                // Retrieve the latest sensor data for the specific plant, ordered by recording time descending.
                // Use FirstOrDefaultAsync after ordering to get the single latest record.
                var dbModel = await _context.Sensordata
                                            .AsNoTracking() // Read-only operation
                                            .Where(d => d.Plantid == plantId)
                                            .OrderByDescending(d => d.Recordedat)
                                            .FirstOrDefaultAsync(); // Get the first (latest) or default (null)

                if (dbModel == null)
                {
                    // This is not necessarily an error, just means no data exists for this plant yet.
                    _logger.LogInformation("No SensorData found for Plant ID {PlantId}.", plantId);
                    return Result<SensorDataEntity>.Failure("No sensor data found for this plant."); // Indicate not found
                }

                var entity = MapToDomainEntity(dbModel);
                if (entity == null) // Defensive check for mapping failure
                {
                    _logger.LogError("Failed to map latest Sensordata DB model for Plant ID {PlantId} to domain entity. DB ID: {DbDataId}", plantId, dbModel.Idsensordata);
                    return Result<SensorDataEntity>.Failure("Error retrieving latest sensor data.");
                }


                _logger.LogInformation("Successfully retrieved latest SensorData with ID {SensorDataId} for Plant ID {PlantId}.", entity.IdSensorData, plantId);
                return Result<SensorDataEntity>.Success(entity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while retrieving the latest SensorData for Plant ID {PlantId}.", plantId);
                return Result<SensorDataEntity>.Failure("An error occurred while retrieving the latest sensor data.");
            }
        }


        // --- Basic/Placeholder implementations for other methods ---

        /// <inheritdoc/>
        public Task<Result<IEnumerable<SensorDataEntity>>> GetAll()
        {
            _logger.LogInformation("GetAll method called for SensorData, but not commonly used/implemented for large datasets.");
            return Task.FromResult(Result <IEnumerable<SensorDataEntity>>.Failure("Operation not commonly used for sensor data."));
        }

        /// <inheritdoc/>
        public Task<Result<bool>> Update(SensorDataEntity entity)
        {
            _logger.LogInformation("Update method called for SensorData ID {Id}, but sensor data is usually append-only.", entity.IdSensorData);
            return Task.FromResult(Result<bool>.Failure("Operation not typically supported for sensor data."));
        }

        /// <inheritdoc/>
        public Task<Result<bool>> Delete(int id)
        {
            _logger.LogInformation("Delete method called for SensorData ID {Id}, but sensor data is usually append-only.", id);
            return Task.FromResult(Result<bool>.Failure("Operation not typically supported for sensor data."));
        }
    }
}