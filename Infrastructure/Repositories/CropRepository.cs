using AngleSharp.Dom;
using ArandanoIRT_Backend.Application.Interfaces.Utilities;
using ArandanoIRT_Backend.Domain.Entities;
using ArandanoIRT_Backend.Domain.IRepositories;
using ArandanoIRT_Backend.Domain.ValueObjects;
using ArandanoIRT_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ArandanoIRT_Backend.Infrastructure.Repositories
{
    /// <summary>
    /// Implements the <see cref="ICropRepository"/> interface, providing data access logic
    /// for crop entities (<see cref="CropEntity"/>) using Entity Framework Core.
    /// </summary>
    public class CropRepository(ApplicationDbContext context, IDateTimeProvider dateTimeProvider, ILogger<CropRepository> logger) : ICropRepository
    {
        private readonly ApplicationDbContext _context = context ?? throw new ArgumentNullException(nameof(context));
        private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
        private readonly ILogger<CropRepository> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // --- Mapping Methods ---
        private CropEntity? MapToDomainEntity(Crop? crop) // Allow nullable input
        {
            if (crop == null) return null;

            var cropEntity = new CropEntity(
                crop.Idcrop,
                crop.Namecrop,
                crop.Addresscrop,
                crop.Cityname,
                crop.Createdat,
                crop.Adminuserid
            );

            // Use reflection to set UpdatedAt (consider adding to constructor if possible)
            var updatedAtProperty = typeof(CropEntity).GetProperty(nameof(CropEntity.UpdatedAt));
            if (updatedAtProperty?.CanWrite == true)
            {
                updatedAtProperty.SetValue(cropEntity, crop.Updatedat, null);
            }
            else
            {
                _logger.LogWarning("Could not set UpdatedAt property via reflection on CropEntity during mapping.");
            }
            return cropEntity;
        }

        private static Crop MapToDbModel(CropEntity entity, Crop? existingCrop = null)
        {
            var crop = existingCrop ?? new Crop();

            // Only set ID if creating (assuming DB generates it)
            if (existingCrop == null)
            {
                // crop.Idcrop = entity.IdCrop; // Let DB handle ID generation usually
            }

            crop.Namecrop = entity.NameCrop;
            crop.Addresscrop = entity.AddressCrop; // Check domain entity property name matches this
            crop.Cityname = entity.CityName;
            crop.Adminuserid = entity.AdminUserId;
            crop.Createdat = entity.CreatedAt;
            crop.Updatedat = entity.UpdatedAt; // Map UpdatedAt for potential update scenario
            return crop;
        }

        // --- Repository Methods ---

        /// <inheritdoc/>
        public async Task<Result<CropEntity>> GetById(int id)
        {
            try
            {
                var crop = await _context.Crop
                                         .AsNoTracking()
                                         .FirstOrDefaultAsync(c => c.Idcrop == id);

                var domainEntity = MapToDomainEntity(crop);
                if (domainEntity == null)
                    return Result<CropEntity>.Failure("Crop not found.");

                return Result<CropEntity>.Success(domainEntity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving crop by ID {Id}.", id);
                return Result<CropEntity>.Failure("Error retrieving crop by ID.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<IEnumerable<CropEntity>>> GetAll()
        {
            try
            {
                var crops = await _context.Crop
                                          .AsNoTracking()
                                          .ToListAsync();
                // Use OfType<CropEntity> to safely handle potential nulls from MapToDomainEntity
                return Result<IEnumerable<CropEntity>>.Success(crops.Select(MapToDomainEntity).OfType<CropEntity>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all crops.");
                return Result<IEnumerable<CropEntity>>.Failure("Error retrieving all crops.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<CropEntity>> Create(CropEntity entity)
        {
            if (entity == null)
                return Result<CropEntity>.Failure("Crop entity cannot be null.");

            try
            {
                var cropDbModel = MapToDbModel(entity); // Map to DB model

                // Set creation timestamp and nullify UpdatedAt
                if (cropDbModel.Createdat == default)
                    cropDbModel.Createdat = _dateTimeProvider.GetUtcNow();
                cropDbModel.Updatedat = null;

                // Add and save
                await _context.Crop.AddAsync(cropDbModel);
                int success = await _context.SaveChangesAsync();

                if (success == 0)
                {
                    _logger.LogWarning("Failed to save new crop to the database. Entity: {@CropEntity}", entity);
                    return Result<CropEntity>.Failure("Failed to save crop to the database.");
                }

                // *** Refactored Part ***
                // Map the *saved* DB model (which now has the ID) back to a *new* domain entity.
                var createdEntity = MapToDomainEntity(cropDbModel);
                if (createdEntity == null) // Should ideally not happen after successful save
                {
                    _logger.LogError("Failed to map newly created crop back to domain entity. DB ID: {DbCropId}", cropDbModel.Idcrop);
                    return Result<CropEntity>.Failure("Failed to map created crop.");
                }

                _logger.LogInformation("Successfully created crop with ID {CropId}.", createdEntity.IdCrop);
                return Result<CropEntity>.Success(createdEntity); // Return the newly mapped entity with the ID
                                                                  // Removed reflection workaround and commented-out code
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error creating crop: {DbError}", dbEx.InnerException?.Message ?? dbEx.Message);
                return Result<CropEntity>.Failure("Database error creating crop.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating crop: {Error}", ex.Message);
                return Result<CropEntity>.Failure("Error creating crop.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<bool>> Update(CropEntity entity)
        {
            if (entity == null)
                return Result<bool>.Failure("Crop entity cannot be null.");
            if (entity.IdCrop <= 0) // Added check for valid ID
                return Result<bool>.Failure("Invalid Crop ID provided for update.");

            try
            {
                // Call helper method to perform the core update logic
                return await FindUpdateAndSaveAsync(entity);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex, "Concurrency conflict updating crop with ID {CropId}.", entity.IdCrop);
                return Result<bool>.Failure("Concurrency conflict updating crop.");
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error updating crop: {DbError}", dbEx.InnerException?.Message ?? dbEx.Message);
                return Result<bool>.Failure("Database error updating crop.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating crop: {Error}", ex.Message);
                return Result<bool>.Failure("Error updating crop.");
            }
        }

        /// <summary>
        /// Finds the existing crop, maps updates, sets timestamp, saves, and returns result.
        /// </summary>
        /// <param name="entity">The domain entity with updated data.</param>
        /// <returns>Result indicating success or failure of the update.</returns>
        private async Task<Result<bool>> FindUpdateAndSaveAsync(CropEntity entity)
        {
            // Find existing entity
            var existingCrop = await _context.Crop.FindAsync(entity.IdCrop);
            if (existingCrop == null)
            {
                _logger.LogWarning("Crop with ID {CropId} not found for update.", entity.IdCrop);
                return Result<bool>.Failure("Crop not found for update.");
            }

            // Map updates from domain entity to tracked DB entity
            MapToDbModel(entity, existingCrop);

            // Set the UpdatedAt timestamp using helper
            SetUpdatedAt(existingCrop);

            // Mark as modified (though FindAsync + modifications usually does this) and save
            _context.Crop.Update(existingCrop); // Explicitly mark update
            int rowsAffected = await _context.SaveChangesAsync();

            // Return result based on rows affected
            if (rowsAffected > 0)
            {
                _logger.LogInformation("Successfully updated crop ID: {CropId}", entity.IdCrop);
                return Result<bool>.Success(true);
            }
            else
            {
                _logger.LogInformation("No changes were detected or saved for crop ID: {CropId}.", entity.IdCrop);
                // Consider Success(false) as no error occurred, just no change needed saving.
                return Result<bool>.Success(false);
            }
        }

        /// <summary>
        /// Sets the UpdatedAt property on the database model based on domain entity value or current time.
        /// </summary>
        /// <param name="existingCrop">The database entity being updated.</param>
        private void SetUpdatedAt(Crop existingCrop)
        {
            existingCrop.Updatedat = _dateTimeProvider.GetUtcNow();
        }


        /// <inheritdoc/>
        public async Task<Result<bool>> Delete(int id)
        {
            try
            {
                var crop = await _context.Crop.FindAsync(id);
                if (crop == null)
                    return Result<bool>.Failure("Crop not found for deletion.");

                _context.Crop.Remove(crop);
                int rowsAffected = await _context.SaveChangesAsync();

                if (rowsAffected > 0)
                {
                    _logger.LogInformation("Successfully deleted crop ID: {CropId}", id);
                    return Result<bool>.Success(true);
                }
                else
                {
                    _logger.LogWarning("Failed to delete crop (no rows affected) ID: {CropId}", id);
                    return Result<bool>.Failure("Failed to delete the crop.");
                }
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error deleting crop (check related records): {DbError}", dbEx.InnerException?.Message ?? dbEx.Message);
                return Result<bool>.Failure("Database error deleting crop (check related records).");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting crop: {Error}", ex.Message);
                return Result<bool>.Failure("Error deleting crop.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<CropEntity>> GetByNameAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                _logger.LogWarning("GetByNameAsync called with null or empty name.");
                return Result<CropEntity>.Failure("Crop name cannot be empty.");
            }

            try
            {
                // Case-insensitive comparison recommended at DB level if possible, else C# ToLower
                var crop = await _context.Crop
                                         .AsNoTracking()
                                         .FirstOrDefaultAsync(c => c.Namecrop.ToLower() == name.ToLower());

                if (crop == null) // Ensure crop is not null before accessing its properties
                {
                    _logger.LogInformation("Crop with name '{CropName}' not found.", name);
                    return Result<CropEntity>.Failure($"Crop with name '{name}' not found.");
                }

                var domainEntity = MapToDomainEntity(crop);
                if (domainEntity == null)
                {
                    _logger.LogInformation("Crop with name '{CropName}' not found.", name);
                    return Result<CropEntity>.Failure($"Crop with name '{name}' not found.");
                }

                _logger.LogInformation("Crop with name '{CropName}' found (ID: {CropId}).", name, crop.Idcrop);
                return Result<CropEntity>.Success(domainEntity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving crop by name '{CropName}'.", name);
                return Result<CropEntity>.Failure("Error retrieving crop by name.");
            }
        }
    }
}