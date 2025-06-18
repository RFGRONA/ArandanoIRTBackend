using ArandanoIRT_Backend.Domain.Entities;
using ArandanoIRT_Backend.Domain.IRepositories;
using ArandanoIRT_Backend.Domain.ValueObjects;
using ArandanoIRT_Backend.Infrastructure.Data; 
using Microsoft.EntityFrameworkCore;

namespace ArandanoIRT_Backend.Infrastructure.Repositories
{
    /// <summary>
    /// Implements the <see cref="IDeviceTokenRepository"/> interface, providing data access logic
    /// for device token entities (<see cref="DeviceTokenEntity"/>) using Entity Framework Core.
    /// </summary>
    public class DeviceTokenRepository : IDeviceTokenRepository
    {
        private readonly ApplicationDbContext _context; 
        private readonly ILogger<DeviceTokenRepository> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DeviceTokenRepository"/> class.
        /// </summary>
        /// <param name="context">The database context.</param>
        /// <param name="logger">The logger instance.</param>
        /// <exception cref="ArgumentNullException">Thrown if context or logger is null.</exception>
        public DeviceTokenRepository(ApplicationDbContext context, ILogger<DeviceTokenRepository> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Maps an Entity Framework Core Devicetoken model to a Domain DeviceTokenEntity.
        /// </summary>
        /// <param name="dbModel">The EF Core model.</param>
        /// <returns>The corresponding Domain Entity.</returns>
        private static DeviceTokenEntity? MapToDomainEntity(Devicetoken? dbModel) 
        {
            if (dbModel == null) return null;

            // Use constructor and reflection for private setters if needed
            var entity = new DeviceTokenEntity(
                dbModel.Iddevicetoken,
                dbModel.Deviceid,
                dbModel.Token, 
                dbModel.Refreshtoken, 
                dbModel.Createdat,
                dbModel.Expiresat,
                dbModel.Revokedat,
                dbModel.Revokedbyip ?? string.Empty, 
                dbModel.Deviceinfo ?? string.Empty, 
                dbModel.Useragent ?? string.Empty 
            );

            return entity;
        }

        /// <summary>
        /// Maps a Domain DeviceTokenEntity to an Entity Framework Core Devicetoken model.
        /// </summary>
        /// <param name="entity">The Domain Entity.</param>
        /// <param name="existingDbModel">Optional existing EF Core model to update.</param>
        /// <returns>The corresponding EF Core model.</returns>
        private static Devicetoken MapToDbModel(DeviceTokenEntity entity, Devicetoken? existingDbModel = null)
        {
            var dbModel = existingDbModel ?? new Devicetoken();

            // Map properties from domain entity to DB model.
            if (existingDbModel != null)
            {
                dbModel.Iddevicetoken = entity.IdDeviceToken; // Keep existing ID for update context
            }

            dbModel.Deviceid = entity.DeviceId;
            dbModel.Token = entity.Token; 
            dbModel.Refreshtoken = entity.RefreshToken; 
            dbModel.Createdat = entity.CreatedAt;
            dbModel.Expiresat = entity.ExpiresAt;
            dbModel.Revokedat = entity.RevokedAt;
            dbModel.Revokedbyip = entity.RevokedByIp;
            dbModel.Deviceinfo = entity.DeviceInfo;
            dbModel.Useragent = entity.UserAgent;

            return dbModel;
        }


        /// <inheritdoc/>
        public async Task<Result<DeviceTokenEntity>> Create(DeviceTokenEntity entity)
        {
            if (entity == null)
            {
                _logger.LogWarning("Attempted to create a null DeviceToken entity.");
                return Result<DeviceTokenEntity>.Failure("Device token entity cannot be null.");
            }

            try
            {
                var dbModel = MapToDbModel(entity);

                await _context.Devicetoken.AddAsync(dbModel);
                int rowsAffected = await _context.SaveChangesAsync();

                if (rowsAffected > 0)
                {
                    var createdEntity = MapToDomainEntity(dbModel);
                    if (createdEntity == null || createdEntity.IdDeviceToken <= 0) // Defensive check
                    {
                        _logger.LogError("Failed to map newly created Devicetoken DB model back to domain entity or ID is missing. DB ID: {DbTokenId}", dbModel.Iddevicetoken);
                        return Result<DeviceTokenEntity>.Failure("Failed to map created device token.");
                    }

                    _logger.LogInformation("Successfully created DeviceToken with ID {DeviceTokenId} for Device ID {DeviceId}.", createdEntity.IdDeviceToken, createdEntity.DeviceId);
                    return Result<DeviceTokenEntity>.Success(createdEntity);
                }
                else
                {
                    _logger.LogWarning("Failed to save new DeviceToken to the database (no rows affected). Entity: {@TokenEntity}", entity);
                    return Result<DeviceTokenEntity>.Failure("Failed to save device token to the database.");
                }
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error creating DeviceToken for Device ID {DeviceId}: {Message}", entity.DeviceId, dbEx.InnerException?.Message ?? dbEx.Message);
                return Result<DeviceTokenEntity>.Failure("Database error occurred while creating the device token.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while creating DeviceToken for Device ID {DeviceId}.", entity.DeviceId);
                return Result<DeviceTokenEntity>.Failure("An error occurred while creating the device token.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<bool>> Delete(int id)
        {
            if (id <= 0)
            {
                _logger.LogWarning("Attempted to delete DeviceToken with invalid ID: {DeviceTokenId}", id);
                return Result<bool>.Failure("Invalid device token ID provided for deletion.");
            }

            try
            {
                var dbModelToDelete = await _context.Devicetoken.FindAsync(id);

                if (dbModelToDelete == null)
                {
                    _logger.LogInformation("DeviceToken with ID {DeviceTokenId} not found for deletion.", id);
                    return Result<bool>.Failure("Device token not found for deletion.");
                }

                _context.Devicetoken.Remove(dbModelToDelete);
                int rowsAffected = await _context.SaveChangesAsync();

                if (rowsAffected > 0)
                {
                    _logger.LogInformation("Successfully deleted DeviceToken ID {DeviceTokenId}.", id);
                    return Result<bool>.Success(true);
                }
                else
                {
                    _logger.LogWarning("Failed to delete DeviceToken ID {DeviceTokenId} (no rows affected).", id);
                    return Result<bool>.Failure("Failed to delete the device token.");
                }
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error deleting DeviceToken ID {DeviceTokenId}: {Message}", id, dbEx.InnerException?.Message ?? dbEx.Message);
                return Result<bool>.Failure("Database error occurred while deleting the device token.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while deleting DeviceToken ID {DeviceTokenId}.", id);
                return Result<bool>.Failure("An error occurred while deleting the device token.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<IEnumerable<DeviceTokenEntity>>> GetAll()
        {
            try
            {
                var dbModels = await _context.Devicetoken
                                             .AsNoTracking()
                                             .ToListAsync();

                var entities = dbModels.Select(MapToDomainEntity).OfType<DeviceTokenEntity>().ToList();

                _logger.LogInformation("Successfully retrieved {Count} DeviceToken records.", entities.Count);
                return Result<IEnumerable<DeviceTokenEntity>>.Success(entities);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while retrieving all DeviceToken records.");
                return Result <IEnumerable<DeviceTokenEntity>>.Failure("An error occurred while retrieving device tokens.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<DeviceTokenEntity>> GetById(int id)
        {
            if (id <= 0)
            {
                _logger.LogWarning("Attempted to retrieve DeviceToken with invalid ID: {DeviceTokenId}", id);
                return Result<DeviceTokenEntity>.Failure("Invalid device token ID provided.");
            }

            try
            {
                var dbModel = await _context.Devicetoken
                                            .AsNoTracking()
                                            .FirstOrDefaultAsync(t => t.Iddevicetoken == id);

                if (dbModel == null)
                {
                    _logger.LogInformation("DeviceToken with ID {DeviceTokenId} not found.", id);
                    return Result<DeviceTokenEntity>.Failure("Device token not found.");
                }

                var entity = MapToDomainEntity(dbModel);
                if (entity == null) // Defensive check
                {
                    _logger.LogError("Failed to map Devicetoken DB model with ID {DeviceTokenId} to domain entity.", id);
                    return Result<DeviceTokenEntity>.Failure("Error retrieving device token details.");
                }


                _logger.LogInformation("Successfully retrieved DeviceToken with ID {DeviceTokenId}.", id);
                return Result<DeviceTokenEntity>.Success(entity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while retrieving DeviceToken by ID {DeviceTokenId}.", id);
                return Result<DeviceTokenEntity>.Failure("An error occurred while retrieving device token details.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<bool>> Update(DeviceTokenEntity entity)
        {
            if (entity == null)
            {
                _logger.LogWarning("Attempted to update a null DeviceToken entity.");
                return Result<bool>.Failure("Device token entity cannot be null for update.");
            }
            if (entity.IdDeviceToken <= 0)
            {
                _logger.LogWarning("Attempted to update DeviceToken with invalid ID: {DeviceTokenId}", entity.IdDeviceToken);
                return Result<bool>.Failure("Invalid device token ID provided for update.");
            }

            try
            {
                var existingDbModel = await _context.Devicetoken.FindAsync(entity.IdDeviceToken);

                if (existingDbModel == null)
                {
                    _logger.LogInformation("DeviceToken with ID {DeviceTokenId} not found for update.", entity.IdDeviceToken);
                    return Result<bool>.Failure("Device token not found for update.");
                }

                // Map updated values onto the existing tracked model.
                MapToDbModel(entity, existingDbModel);

                int rowsAffected = await _context.SaveChangesAsync();

                if (rowsAffected > 0)
                {
                    _logger.LogInformation("Successfully updated DeviceToken with ID {DeviceTokenId}.", entity.IdDeviceToken);
                    return Result<bool>.Success(true);
                }
                else
                {
                    _logger.LogInformation("DeviceToken with ID {DeviceTokenId} found, but no changes were saved during update.", entity.IdDeviceToken);
                    return Result<bool>.Success(false); // Indicate no changes saved
                }
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex, "Concurrency conflict updating DeviceToken ID {DeviceTokenId}.", entity.IdDeviceToken);
                return Result<bool>.Failure("Concurrency conflict updating device token. Please try again.");
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error updating DeviceToken ID {DeviceTokenId}: {Message}", entity.IdDeviceToken, dbEx.InnerException?.Message ?? dbEx.Message);
                return Result<bool>.Failure("Database error occurred while updating the device token.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while updating DeviceToken ID {DeviceTokenId}.", entity.IdDeviceToken);
                return Result<bool>.Failure("An error occurred while updating the device token.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<DeviceTokenEntity>> GetByTokenAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                _logger.LogWarning("Attempted to retrieve DeviceToken with empty or null token string.");
                return Result<DeviceTokenEntity>.Failure("Token string cannot be empty.");
            }

            try
            {
                // Find the token by its string value. Note property name in DB model might differ.
                // Assuming the token string is stored in the 'Token' column based on the entity definition.
                var dbModel = await _context.Devicetoken
                                            .AsNoTracking()
                                            .FirstOrDefaultAsync(t => t.Token == token); 

                if (dbModel == null)
                {
                    _logger.LogInformation("DeviceToken with token value (hashed/opaque) not found.");
                    return Result<DeviceTokenEntity>.Failure("Device token not found or invalid.");
                }

                var entity = MapToDomainEntity(dbModel);
                if (entity == null) // Defensive check
                {
                    _logger.LogError("Failed to map Devicetoken DB model by token to domain entity. DB ID: {DbTokenId}", dbModel.Iddevicetoken);
                    return Result<DeviceTokenEntity>.Failure("Error retrieving device token.");
                }


                _logger.LogInformation("Successfully retrieved DeviceToken by token value (ID: {DeviceTokenId}).", entity.IdDeviceToken);
                return Result<DeviceTokenEntity>.Success(entity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while retrieving DeviceToken by token string.");
                return Result<DeviceTokenEntity>.Failure("An error occurred while retrieving the device token.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<IEnumerable<DeviceTokenEntity>>> GetByDeviceIdAsync(int deviceId)
        {
            if (deviceId <= 0)
            {
                _logger.LogWarning("Attempted to retrieve DeviceTokens with invalid Device ID: {DeviceId}", deviceId);
                return Result<IEnumerable<DeviceTokenEntity>>.Failure("Invalid device ID provided for retrieving tokens.");
            }

            try
            {
                // Retrieve all tokens associated with a specific device ID.
                var dbModels = await _context.Devicetoken
                                             .AsNoTracking()
                                             .Where(t => t.Deviceid == deviceId)
                                             .ToListAsync();

                var entities = dbModels.Select(MapToDomainEntity).OfType<DeviceTokenEntity>().ToList();

                _logger.LogInformation("Successfully retrieved {Count} DeviceToken records for Device ID {DeviceId}.", entities.Count, deviceId);
                return Result<IEnumerable<DeviceTokenEntity>>.Success(entities);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while retrieving DeviceToken records for Device ID {DeviceId}.", deviceId);
                return Result<IEnumerable<DeviceTokenEntity>>.Failure("An error occurred while retrieving device tokens.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<bool>> RevokeByTokenAsync(string token, DateTime revokedAt, string? revokedByIp)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                _logger.LogWarning("Attempted to revoke DeviceToken with empty or null token string.");
                return Result<bool>.Failure("Token string cannot be empty for revocation.");
            }

            try
            {
                // Find the token by its string value. Must be tracked for update.
                var dbModelToRevoke = await _context.Devicetoken
                                                    .FirstOrDefaultAsync(t => t.Token == token); 

                if (dbModelToRevoke == null)
                {
                    _logger.LogInformation("DeviceToken with token value not found for revocation.");
                    // Consider success if the token doesn't exist - the desired state is achieved.
                    return Result<bool>.Success(true);
                }

                // Check if it's already revoked to avoid unnecessary DB updates
                if (dbModelToRevoke.Revokedat != null)
                {
                    _logger.LogInformation("DeviceToken with ID {DeviceTokenId} is already revoked.", dbModelToRevoke.Iddevicetoken);
                    return Result<bool>.Success(true); // Already revoked, consider successful
                }

                // Mark the token as revoked.
                dbModelToRevoke.Revokedat = revokedAt;
                dbModelToRevoke.Revokedbyip = revokedByIp; // Nullable

                // Save changes.
                int rowsAffected = await _context.SaveChangesAsync();

                if (rowsAffected > 0)
                {
                    _logger.LogInformation("Successfully revoked DeviceToken ID {DeviceTokenId} for Device ID {DeviceId}.", dbModelToRevoke.Iddevicetoken, dbModelToRevoke.Deviceid);
                    return Result<bool>.Success(true);
                }
                else
                {
                    // This might happen if entity was tracked but no changes were detected (e.g., RevokedAt was already set externally)
                    _logger.LogInformation("DeviceToken with ID {DeviceTokenId} found, but no changes were saved during revocation (possibly already revoked).", dbModelToRevoke.Iddevicetoken);
                    return Result<bool>.Success(false); // Indicate no changes saved, but the operation didn't fail.
                }
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex, "Concurrency conflict revoking DeviceToken by token string.");
                return Result<bool>.Failure("Concurrency conflict revoking device token. Please try again.");
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error revoking DeviceToken by token string: {Message}", dbEx.InnerException?.Message ?? dbEx.Message);
                return Result<bool>.Failure("Database error occurred while revoking the device token.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while revoking DeviceToken by token string.");
                return Result<bool>.Failure("An error occurred while revoking the device token.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<bool>> RevokeByDeviceIdAsync(int deviceId, DateTime revokedAt, string? revokedByIp)
        {
            if (deviceId <= 0)
            {
                _logger.LogWarning("Attempted to revoke DeviceTokens with invalid Device ID: {DeviceId}", deviceId);
                return Result<bool>.Failure("Invalid device ID provided for revocation.");
            }

            try
            {
                // Find all active tokens associated with the device ID. Must be tracked for update.
                var dbModelsToRevoke = await _context.Devicetoken
                                                    .Where(t => t.Deviceid == deviceId && t.Revokedat == null) // Find only active tokens
                                                    .ToListAsync();

                if (dbModelsToRevoke.Count == 0)
                {
                    _logger.LogInformation("No active DeviceTokens found for Device ID {DeviceId} to revoke.", deviceId);
                    return Result<bool>.Success(true); // No tokens to revoke, consider successful.
                }

                _logger.LogInformation("Revoking {Count} active DeviceTokens for Device ID {DeviceId}.", dbModelsToRevoke.Count, deviceId);

                // Mark each found token as revoked.
                foreach (var dbModel in dbModelsToRevoke)
                {
                    dbModel.Revokedat = revokedAt;
                    dbModel.Revokedbyip = revokedByIp;
                }
                int rowsAffected = await _context.SaveChangesAsync();

                // Check if the number of affected rows is at least the number of entities found.
                // It might be more if other updates were pending, but we expect at least this many.
                if (rowsAffected >= dbModelsToRevoke.Count)
                {
                    _logger.LogInformation("Successfully revoked {RowsAffected} DeviceTokens for Device ID {DeviceId}.", rowsAffected, deviceId);
                    return Result<bool>.Success(true);
                }
                else
                {
                    _logger.LogWarning("Attempted to revoke {ExpectedCount} tokens for Device ID {DeviceId}, but only {RowsAffected} rows affected. Potential issue.", dbModelsToRevoke.Count, deviceId, rowsAffected);
                    return Result<bool>.Failure("Failed to revoke all expected device tokens.");
                }

            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex, "Concurrency conflict revoking DeviceTokens by Device ID {DeviceId}.", deviceId);
                return Result<bool>.Failure("Concurrency conflict revoking device tokens. Please try again.");
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error revoking DeviceTokens by Device ID {DeviceId}: {Message}", deviceId, dbEx.InnerException?.Message ?? dbEx.Message);
                return Result<bool>.Failure("Database error occurred while revoking device tokens.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while revoking DeviceTokens by Device ID {DeviceId}.", deviceId);
                return Result<bool>.Failure("An error occurred while revoking device tokens.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<bool>> DeleteByDeviceIdAsync(int deviceId)
        {
            if (deviceId <= 0)
            {
                _logger.LogWarning("Attempted to delete DeviceTokens with invalid Device ID: {DeviceId}", deviceId);
                return Result<bool>.Failure("Invalid device ID provided for deletion.");
            }

            try
            {
                // Find all tokens associated with the device ID.
                var dbModelsToDelete = await _context.Devicetoken
                                                   .Where(t => t.Deviceid == deviceId)
                                                   .ToListAsync();

                if (dbModelsToDelete.Count == 0)
                {
                    _logger.LogInformation("No DeviceTokens found for Device ID {DeviceId} to delete.", deviceId);
                    return Result<bool>.Success(true); // No tokens to delete, consider successful.
                }

                _logger.LogInformation("Deleting {Count} DeviceTokens for Device ID {DeviceId}.", dbModelsToDelete.Count, deviceId);

                _context.Devicetoken.RemoveRange(dbModelsToDelete);
                int rowsAffected = await _context.SaveChangesAsync();

                // Check if the number of affected rows is at least the number of entities found.
                if (rowsAffected >= dbModelsToDelete.Count)
                {
                    _logger.LogInformation("Successfully deleted {RowsAffected} DeviceTokens for Device ID {DeviceId}.", rowsAffected, deviceId);
                    return Result<bool>.Success(true);
                }
                else
                {
                    _logger.LogWarning("Attempted to delete {ExpectedCount} tokens for Device ID {DeviceId}, but only {RowsAffected} rows affected. Potential issue.", dbModelsToDelete.Count, deviceId, rowsAffected);
                    return Result<bool>.Failure("Failed to delete all device tokens.");
                }
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error deleting DeviceTokens by Device ID {DeviceId}: {Message}", deviceId, dbEx.InnerException?.Message ?? dbEx.Message);
                return Result<bool>.Failure("Database error occurred while deleting device tokens.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while deleting DeviceTokens by Device ID {DeviceId}.", deviceId);
                return Result<bool>.Failure("An error occurred while deleting device tokens.");
            }
        }
    }
}