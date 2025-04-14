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
    /// Implements the <see cref="ICropRepository"/> interface, providing data access logic
    /// for crop entities (<see cref="CropEntity"/>) using Entity Framework Core.
    /// </summary>
    public class CropRepository(ApplicationDbContext context, IDateTimeProvider dateTimeProvider) : ICropRepository
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
        /// Static Serilog logger instance specific to this repository.
        /// </summary>
        private readonly Serilog.ILogger _logger = Log.ForContext<CropRepository>();

        /// <summary>
        /// Maps a database context <see cref="Crop"/> entity to a domain <see cref="CropEntity"/>.
        /// </summary>
        /// <param name="crop">The database entity instance.</param>
        /// <returns>The mapped domain entity instance, or <c>null</c> if input is null (throws NullReferenceException due to null! usage).</returns>
        /// <remarks>
        /// Uses reflection to set the <c>UpdatedAt</c> property on the domain entity after construction.
        /// Note: Handles potential null AdminUserId from DB. Assumes domain entity constructor handles required fields correctly.
        /// </remarks>
        private CropEntity MapToDomainEntity(Crop crop)
        {
            // Guards against null input, though using null forgiving operator suggests it shouldn't be null.
            if (crop == null) return null!;
            // Creates domain entity using its constructor.
            var cropEntity = new CropEntity(
                crop.Idcrop,
                crop.Namecrop,
                crop.Addresscrop, 
                crop.Cityname,
                crop.Createdat,
                crop.Adminuserid
            );
            // Uses reflection to set the UpdatedAt property, as it's not in the domain entity constructor.
            var updatedAtProperty = typeof(CropEntity).GetProperty(nameof(CropEntity.UpdatedAt));
            if (updatedAtProperty?.CanWrite ?? false)
            {
                updatedAtProperty.SetValue(cropEntity, crop.Updatedat, null);
            }
            else
            {
                _logger.Warning("Could not set UpdatedAt property via reflection on CropEntity during mapping.");
            }

            return cropEntity;
        }

        /// <summary>
        /// Maps a domain <see cref="CropEntity"/> to a database context <see cref="Crop"/> entity.
        /// Updates existing instance if provided, otherwise creates a new one.
        /// </summary>
        /// <param name="entity">The domain entity instance.</param>
        /// <param name="existingCrop">Optional. The existing database entity to update.</param>
        /// <returns>The mapped or updated database entity instance.</returns>
        /// <remarks>
        /// Maps domain entity properties to the corresponding database model properties.
        /// Note: This code maps <c>entity.AddressCrop</c> (standard spelling) to <c>crop.Addrescrop</c> (database typo).
        /// Ensure the domain <see cref="CropEntity"/> indeed has an <c>AddressCrop</c> property or adjust mapping.
        /// The previously provided <c>CropEntity</c> definition had <c>AddresCrop</c> (with typo).
        /// This mapping might cause issues if the domain entity property name doesn't match <c>AddressCrop</c>.
        /// </remarks>
        private Crop MapToDbModel(CropEntity entity, Crop? existingCrop = null)
        {
            // Uses existing instance or creates a new one.
            var crop = existingCrop ?? new Crop();
            // Maps properties from domain entity to database model.
            crop.Idcrop = entity.IdCrop; // Usually ID is not set manually unless updating.
            crop.Namecrop = entity.NameCrop;
            crop.Addresscrop = entity.AddressCrop; 
            crop.Cityname = entity.CityName;
            crop.Adminuserid = entity.AdminUserId;
            crop.Createdat = entity.CreatedAt; // Maps CreatedAt for potential updates if needed.
            crop.Updatedat = entity.UpdatedAt; // Maps UpdatedAt for updates.
            return crop;
        }

        /// <inheritdoc/>
        public async Task<Result<CropEntity>> GetById(int id)
        {
            try
            {
                // Retrieves the crop entity by ID without tracking changes.
                var crop = await _context.Crop
                                       .AsNoTracking()
                                       .FirstOrDefaultAsync(c => c.Idcrop == id);
                // Returns failure if not found.
                if (crop == null)
                    return Result<CropEntity>.Failure("Crop not found.");
                // Maps to domain entity and returns success.
                return Result<CropEntity>.Success(MapToDomainEntity(crop));
            }
            catch (Exception ex)
            {
                // Logs and returns failure on error.
                _logger.Error(ex, "Error retrieving crop by ID {Id}.", id); 
                return Result<CropEntity>.Failure("Error retrieving crop by ID.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<IEnumerable<CropEntity>>> GetAll()
        {
            try
            {
                // Retrieves all crop entities without tracking changes.
                var crops = await _context.Crop
                                          .AsNoTracking()
                                          .ToListAsync();
                // Maps the list to domain entities and returns success.
                return Result<IEnumerable<CropEntity>>.Success(crops.Select(MapToDomainEntity));
            }
            catch (Exception ex)
            {
                // Logs and returns failure on error.
                _logger.Error(ex, "Error retrieving all crops."); 
                return Result<IEnumerable<CropEntity>>.Failure("Error retrieving all crops.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<CropEntity>> Create(CropEntity entity)
        {
            // Basic null check for the input domain entity.
            if (entity == null)
                return Result<CropEntity>.Failure("Crop entity cannot be null.");
            try
            {
                // Maps the domain entity to the database model.
                var cropDbModel = MapToDbModel(entity);
                // Sets CreatedAt timestamp if not already set.
                if (cropDbModel.Createdat == default)
                    cropDbModel.Createdat = _dateTimeProvider.GetUtcNow();
                // UpdatedAt should be null on creation.
                cropDbModel.Updatedat = null;

                // Adds the new model to the context.
                await _context.Crop.AddAsync(cropDbModel);
                // Saves changes to the database.
                int success = await _context.SaveChangesAsync();

                // Returns failure if no rows were affected (save failed).
                if (success == 0)
                    return Result<CropEntity>.Failure("Failed to save crop to the database.");

                // --- Workaround: Update domain entity ID post-save ---
                // Reflects the database-generated ID back onto the input domain entity.
                var idProperty = typeof(CropEntity).GetProperty(nameof(CropEntity.IdCrop));
                if (idProperty?.CanWrite ?? false)
                {
                    idProperty.SetValue(entity, cropDbModel.Idcrop, null);
                }
                else
                {
                    _logger.Warning("Could not set IdCrop on domain entity after creation.");
                    // Consider returning a newly mapped entity: return Result<CropEntity>.Success(MapToDomainEntity(cropDbModel));
                }
                // --- End Workaround ---

                // Returns success with the potentially updated domain entity.
                return Result<CropEntity>.Success(entity);
            }
            catch (DbUpdateException dbEx)
            {
                _logger.Error(dbEx, "Database error creating crop: {DbError}", dbEx.InnerException?.Message ?? dbEx.Message); 
                return Result<CropEntity>.Failure("Database error creating crop.");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error creating crop: {Error}", ex.Message); 
                return Result<CropEntity>.Failure("Error creating crop.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<bool>> Update(CropEntity entity)
        {
            // Basic null check for the input domain entity.
            if (entity == null)
                return Result<bool>.Failure("Crop entity cannot be null.");
            try
            {
                // Finds the existing database entity by ID using FindAsync (tracked).
                var existingCrop = await _context.Crop.FindAsync(entity.IdCrop);
                // Returns failure if the entity to update is not found.
                if (existingCrop == null)
                    return Result<bool>.Failure("Crop not found for update.");

                // Maps properties from the domain entity onto the tracked database entity.
                // Note: Pay attention to the Address/Addres mapping comment in MapToDbModel.
                MapToDbModel(entity, existingCrop);
                // Ensures UpdatedAt timestamp is set or updated.
                // Sets UpdatedAt to the value from the entity if provided and later than existing, otherwise set to now.
                if (existingCrop.Updatedat == null || (entity.UpdatedAt.HasValue && existingCrop.Updatedat < entity.UpdatedAt.Value))
                    existingCrop.Updatedat = entity.UpdatedAt ?? _dateTimeProvider.GetUtcNow();
                // If entity.UpdatedAt is null or earlier, still update to 'now' to reflect the update operation time.
                else if (!entity.UpdatedAt.HasValue || existingCrop.Updatedat >= entity.UpdatedAt.Value) // Added case if UpdateAt wasn't changed in request
                    existingCrop.Updatedat = _dateTimeProvider.GetUtcNow();


                // Marks the entity as modified explicitly.
                _context.Crop.Update(existingCrop);
                // Saves changes to the database.
                int rowsAffected = await _context.SaveChangesAsync();

                // Returns success if rows were affected, failure otherwise (e.g., no actual changes detected by EF).
                return rowsAffected > 0
                    ? Result<bool>.Success(true)
                    : Result<bool>.Failure("No changes were detected or saved for the crop.");
            }
            catch (DbUpdateConcurrencyException ex) // Handles concurrency conflicts.
            {
                _logger.Error(ex, "Concurrency conflict updating crop with ID {CropId}.", entity.IdCrop); 
                return Result<bool>.Failure("Concurrency conflict updating crop.");
            }
            catch (DbUpdateException dbEx) // Handles other database update errors.
            {
                _logger.Error(dbEx, "Database error updating crop: {DbError}", dbEx.InnerException?.Message ?? dbEx.Message); 
                return Result<bool>.Failure("Database error updating crop.");
            }
            catch (Exception ex) // Handles general errors.
            {
                _logger.Error(ex, "Error updating crop: {Error}", ex.Message); 
                return Result<bool>.Failure("Error updating crop.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<bool>> Delete(int id)
        {
            try
            {
                // Finds the entity by ID using FindAsync (tracked).
                var crop = await _context.Crop.FindAsync(id);
                // Returns failure if the entity is not found.
                if (crop == null)
                    return Result<bool>.Failure("Crop not found for deletion.");

                // Removes the entity from the context.
                _context.Crop.Remove(crop);
                // Saves changes to the database.
                int rowsAffected = await _context.SaveChangesAsync();

                // Returns success if rows were affected, failure otherwise.
                return rowsAffected > 0
                    ? Result<bool>.Success(true)
                    : Result<bool>.Failure("Failed to delete the crop (no rows affected).");
            }
            catch (DbUpdateException dbEx) // Handles database deletion errors (e.g., foreign key constraints).
            {
                _logger.Error(dbEx, "Database error deleting crop (check related records): {DbError}", dbEx.InnerException?.Message ?? dbEx.Message); 
                return Result<bool>.Failure("Database error deleting crop (check related records).");
            }
            catch (Exception ex) // Handles general errors.
            {
                _logger.Error(ex, "Error deleting crop: {Error}", ex.Message); 
                return Result<bool>.Failure("Error deleting crop.");
            }
        }
    }
}