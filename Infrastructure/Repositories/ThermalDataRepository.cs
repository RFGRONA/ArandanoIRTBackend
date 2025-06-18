using ArandanoIRT_Backend.Domain.Entities;
using ArandanoIRT_Backend.Domain.IRepositories;
using ArandanoIRT_Backend.Domain.ValueObjects; 
using ArandanoIRT_Backend.Infrastructure.Data; 
using Microsoft.EntityFrameworkCore;

namespace ArandanoIRT_Backend.Infrastructure.Repositories
{
    /// <summary>
    /// Implements the <see cref="IThermalDataRepository"/> interface, providing data access logic
    /// for thermal data entities (<see cref="ThermalDataEntity"/>) using Entity Framework Core.
    /// </summary>
    public class ThermalDataRepository : IThermalDataRepository
    {
        private readonly ApplicationDbContext _context; 
        private readonly ILogger<ThermalDataRepository> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ThermalDataRepository"/> class.
        /// </summary>
        /// <param name="context">The database context.</param>
        /// <param name="logger">The logger instance.</param>
        /// <exception cref="ArgumentNullException">Thrown if context or logger is null.</exception>
        public ThermalDataRepository(ApplicationDbContext context, ILogger<ThermalDataRepository> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Maps an Entity Framework Core Thermaldata model to a Domain ThermalDataEntity.
        /// </summary>
        /// <param name="dbModel">The EF Core model.</param>
        /// <returns>The corresponding Domain Entity.</returns>
        private static ThermalDataEntity? MapToDomainEntity(Thermaldata? dbModel) // Make input nullable
        {
            if (dbModel == null) return null;

            // Ensure Rgbimagedata is not null before passing it to the constructor
            var rgbImageData = dbModel.Rgbimagedata ?? [];

            return new ThermalDataEntity(
                dbModel.Idthermaldata,
                dbModel.Thermalimagedata.ToString() ?? string.Empty,
                rgbImageData, 
                dbModel.Recordedat,
                dbModel.Plantid,
                dbModel.Cropid
            );
        }

        /// <summary>
        /// Maps a Domain ThermalDataEntity to an Entity Framework Core Thermaldata model.
        /// </summary>
        /// <param name="entity">The Domain Entity.</param>
        /// <param name="existingDbModel">Optional existing EF Core model to update (less common for thermal data).</param>
        /// <returns>The corresponding EF Core model.</returns>
        private static Thermaldata MapToDbModel(ThermalDataEntity entity, Thermaldata? existingDbModel = null)
        {
            var dbModel = existingDbModel ?? new Thermaldata();

            // Map properties from domain entity to DB model. ID is DB generated on Create.
            if (existingDbModel != null)
            {
                dbModel.Idthermaldata = entity.IdThermalData; // Keep existing ID for update context
            }

            // Mapping string to JSONB. Ensure your EF Core provider handles this correctly.
            // Depending on provider and EF version, you might need specific configuration or converters.
            dbModel.Thermalimagedata = entity.ThermalImageData; // Assuming direct string mapping works for JSONB column

            dbModel.Rgbimagedata = [.. entity.RgbImageData]; // Convert IReadOnlyList<byte> back to byte[]

            dbModel.Recordedat = entity.RecordedAt;
            dbModel.Plantid = entity.PlantId;
            dbModel.Cropid = entity.CropId;

            return dbModel;
        }

        /// <inheritdoc/>
        public async Task<Result<ThermalDataEntity>> Create(ThermalDataEntity entity)
        {
            if (entity == null)
            {
                _logger.LogWarning("Attempted to create a null ThermalData entity.");
                return Result<ThermalDataEntity>.Failure("Thermal data entity cannot be null.");
            }

            try
            {
                var dbModel = MapToDbModel(entity);

                await _context.Thermaldata.AddAsync(dbModel);
                int rowsAffected = await _context.SaveChangesAsync();

                if (rowsAffected > 0)
                {
                    var createdEntity = MapToDomainEntity(dbModel);
                    if (createdEntity == null || createdEntity.IdThermalData <= 0) // Defensive check
                    {
                        _logger.LogError("Failed to map newly created Thermaldata DB model back to domain entity or ID is missing. DB ID: {DbDataId}", dbModel.Idthermaldata);
                        return Result<ThermalDataEntity>.Failure("Failed to map created thermal data.");
                    }

                    _logger.LogInformation("Successfully created ThermalData with ID {ThermalDataId} for Plant ID {PlantId}.", createdEntity.IdThermalData, createdEntity.PlantId);
                    return Result<ThermalDataEntity>.Success(createdEntity);
                }
                else
                {
                    _logger.LogWarning("Failed to save new ThermalData to the database (no rows affected). Entity: {@DataEntity}", entity);
                    return Result<ThermalDataEntity>.Failure("Failed to save thermal data to the database.");
                }
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error creating ThermalData for Plant ID {PlantId}: {Message}", entity.PlantId, dbEx.InnerException?.Message ?? dbEx.Message);
                return Result<ThermalDataEntity>.Failure("Database error occurred while creating the thermal data.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while creating ThermalData for Plant ID {PlantId}.", entity.PlantId);
                return Result<ThermalDataEntity>.Failure("An error occurred while creating the thermal data.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<ThermalDataEntity>> GetById(int id)
        {
            if (id <= 0)
            {
                _logger.LogWarning("Attempted to retrieve ThermalData with invalid ID: {ThermalDataId}", id);
                return Result<ThermalDataEntity>.Failure("Invalid thermal data ID provided.");
            }

            try
            {
                var dbModel = await _context.Thermaldata
                                            .AsNoTracking() // Read-only operation
                                            .FirstOrDefaultAsync(d => d.Idthermaldata == id);

                if (dbModel == null)
                {
                    _logger.LogInformation("ThermalData with ID {ThermalDataId} not found.", id);
                    return Result<ThermalDataEntity>.Failure("Thermal data not found.");
                }

                var entity = MapToDomainEntity(dbModel);
                if (entity == null) // Defensive check for mapping failure
                {
                    _logger.LogError("Failed to map Thermaldata DB model with ID {ThermalDataId} to domain entity.", id);
                    return Result<ThermalDataEntity>.Failure("Error retrieving thermal data details.");
                }


                _logger.LogInformation("Successfully retrieved ThermalData with ID {ThermalDataId}.", id);
                return Result<ThermalDataEntity>.Success(entity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while retrieving ThermalData by ID {ThermalDataId}.", id);
                return Result<ThermalDataEntity>.Failure("An error occurred while retrieving thermal data details.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<IEnumerable<ThermalDataEntity>>> GetAllByPlantIdAsync(int plantId)
        {
            if (plantId <= 0)
            {
                _logger.LogWarning("Attempted to retrieve ThermalData with invalid Plant ID: {PlantId}", plantId);
                return Result<IEnumerable<ThermalDataEntity>>.Failure("Invalid plant ID provided for retrieving thermal data.");
            }

            try
            {
                // Retrieve all thermal data for the specific plant, ordered by recording time descending.
                var dbModels = await _context.Thermaldata
                                             .AsNoTracking() // Read-only operation
                                             .Where(d => d.Plantid == plantId)
                                             .OrderByDescending(d => d.Recordedat)
                                             .ToListAsync();

                // Map the list of database models to domain entities.
                var entities = dbModels.Select(MapToDomainEntity).OfType<ThermalDataEntity>().ToList();


                _logger.LogInformation("Successfully retrieved {Count} ThermalData records for Plant ID {PlantId}.", entities.Count, plantId);
                return Result<IEnumerable<ThermalDataEntity>>.Success(entities);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while retrieving ThermalData records for Plant ID {PlantId}.", plantId);
                return Result<IEnumerable<ThermalDataEntity>>.Failure("An error occurred while retrieving thermal data for the plant.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<ThermalDataEntity>> GetLatestByPlantIdAsync(int plantId)
        {
            if (plantId <= 0)
            {
                _logger.LogWarning("Attempted to retrieve latest ThermalData with invalid Plant ID: {PlantId}", plantId);
                return Result<ThermalDataEntity>.Failure("Invalid plant ID provided for retrieving latest thermal data.");
            }

            try
            {
                // Retrieve the latest thermal data for the specific plant, ordered by recording time descending.
                // Use FirstOrDefaultAsync after ordering to get the single latest record.
                var dbModel = await _context.Thermaldata
                                            .AsNoTracking() // Read-only operation
                                            .Where(d => d.Plantid == plantId)
                                            .OrderByDescending(d => d.Recordedat)
                                            .FirstOrDefaultAsync(); // Get the first (latest) or default (null)

                if (dbModel == null)
                {
                    // This is not necessarily an error, just means no data exists for this plant yet.
                    _logger.LogInformation("No ThermalData found for Plant ID {PlantId}.", plantId);
                    return Result<ThermalDataEntity>.Failure("No thermal data found for this plant."); // Indicate not found
                }

                var entity = MapToDomainEntity(dbModel);
                if (entity == null) // Defensive check for mapping failure
                {
                    _logger.LogError("Failed to map latest Thermaldata DB model for Plant ID {PlantId} to domain entity. DB ID: {DbDataId}", plantId, dbModel.Idthermaldata);
                    return Result<ThermalDataEntity>.Failure("Error retrieving latest thermal data.");
                }


                _logger.LogInformation("Successfully retrieved latest ThermalData with ID {ThermalDataId} for Plant ID {PlantId}.", entity.IdThermalData, plantId);
                return Result<ThermalDataEntity>.Success(entity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while retrieving the latest ThermalData for Plant ID {PlantId}.", plantId);
                return Result<ThermalDataEntity>.Failure("An error occurred while retrieving the latest thermal data.");
            }
        }


        // --- Basic/Placeholder implementations for other methods ---

        /// <inheritdoc/>
        public Task<Result<IEnumerable<ThermalDataEntity>>> GetAll()
        {
            _logger.LogInformation("GetAll method called for ThermalData, but not commonly used/implemented for large datasets.");
            return Task.FromResult(Result <IEnumerable<ThermalDataEntity>>.Failure("Operation not commonly used for thermal data."));
        }

        /// <inheritdoc/>
        public Task<Result<bool>> Update(ThermalDataEntity entity)
        {
            _logger.LogInformation("Update method called for ThermalData ID {Id}, but thermal data is usually append-only.", entity.IdThermalData);
            return Task.FromResult(Result<bool>.Failure("Operation not typically supported for thermal data."));
        }

        /// <inheritdoc/>
        public Task<Result<bool>> Delete(int id)
        {
            _logger.LogInformation("Delete method called for ThermalData ID {Id}, but thermal data is usually append-only.", id);
            return Task.FromResult(Result<bool>.Failure("Operation not typically supported for thermal data."));
        }
    }
}