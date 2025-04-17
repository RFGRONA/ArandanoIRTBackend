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
    /// Implements the <see cref="IRefreshTokenRepository"/> interface, providing data access logic
    /// for refresh token entities (<see cref="RefreshTokenEntity"/>) using Entity Framework Core.
    /// </summary>
    public class RefreshTokenRepository(ApplicationDbContext context, IDateTimeProvider dateTimeProvider, ILogger<RefreshTokenRepository> logger) : IRefreshTokenRepository
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
        /// Logger instance for logging operations and errors.
        /// </summary>
        private readonly ILogger<RefreshTokenRepository> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        /// <summary>
        /// Maps a database context <see cref="Refreshtoken"/> entity to a domain <see cref="RefreshTokenEntity"/>.
        /// </summary>
        /// <param name="token">The database entity instance.</param>
        /// <returns>The mapped domain entity instance, or <c>null</c> if input is null (throws NullReferenceException due to null! usage).</returns>
        /// <remarks>
        /// Handles potential null values for string properties from the database by mapping them to empty strings.
        /// Maps the database <c>Refreshtoken1</c> property to the domain <c>Token</c> property.
        /// </remarks>
        private static RefreshTokenEntity MapToDomainEntity(Refreshtoken token)
        {
            // Guard against null input.
            if (token == null) return null!; // Consider throwing ArgumentNullException if null is truly unexpected.
            return new RefreshTokenEntity(
                token.Idrefreshtoken,
                token.Session,
                token.Refreshtoken1, 
                token.Deviceinfo, 
                token.Ipaddress,   
                token.Useragent,  
                token.Createdat,
                token.Expiresat,
                token.Revokedat,
                token.Revokedbyip,           
                token.Replacedbytoken,       
                token.Personid               
            );
        }

        /// <summary>
        /// Maps a domain <see cref="RefreshTokenEntity"/> to a database context <see cref="Refreshtoken"/> entity.
        /// Updates existing instance if provided, otherwise creates a new one.
        /// </summary>
        /// <param name="entity">The domain entity instance.</param>
        /// <param name="existingToken">Optional. The existing database entity to update.</param>
        /// <returns>The mapped or updated database entity instance.</returns>
        /// <remarks>Maps the domain <c>Token</c> property to the database <c>Refreshtoken1</c> property.</remarks>
        private static Refreshtoken MapToDbModel(RefreshTokenEntity entity, Refreshtoken? existingToken = null)
        {
            // Uses existing instance or creates a new one.
            var token = existingToken ?? new Refreshtoken();
            // Maps properties from domain entity to database model.
            token.Idrefreshtoken = entity.IdRefreshToken; // Usually ID is not set manually unless updating.
            token.Session = entity.Session;
            token.Refreshtoken1 = entity.Token; // Maps domain Token to DB Refreshtoken1.
            token.Deviceinfo = entity.DeviceInfo;
            token.Ipaddress = entity.IpAddress;
            token.Useragent = entity.UserAgent;
            token.Createdat = entity.CreatedAt;
            token.Expiresat = entity.ExpiresAt;
            token.Revokedat = entity.RevokedAt;
            token.Revokedbyip = entity.RevokedByIp; // Maps nullable string?.
            token.Replacedbytoken = entity.ReplacedByToken; // Maps nullable string?.
            token.Personid = entity.PersonId; // Maps nullable int?.
            return token;
        }

        /// <inheritdoc/>
        public async Task<Result<bool>> RevokeBySessionIdAsync(long sessionId, string revokedByIp)
        {
            try
            {
                var now = _dateTimeProvider.GetUtcNow(); // Use consistent timestamping for checks and revocation.

                // Find all active (non-revoked, non-expired) tokens for the given session ID.
                // Uses tracking as these entities will be updated.
                var tokensToRevoke = await _context.Refreshtoken
                                               .Where(rt => rt.Session == sessionId && rt.Revokedat == null && rt.Expiresat > now)
                                               .ToListAsync();

                // If no active tokens found for the session, consider the operation successful.
                if (!tokensToRevoke.Any())
                {
                    _logger.LogInformation("No active refresh tokens found to revoke for session ID {SessionId}.", sessionId);
                    return Result<bool>.Success(true); // Nothing to revoke is considered success.
                }

                _logger.LogInformation("Found {Count} active refresh tokens for session ID {SessionId}. Attempting revocation.", tokensToRevoke.Count, sessionId);

                // Mark each found token as revoked.
                foreach (var token in tokensToRevoke)
                {
                    token.Revokedat = now;
                    token.Revokedbyip = revokedByIp;
                    // We don't set ReplacedByToken here as this is a mass revocation, not rotation.
                }

                // Marks all modified tokens in the context for update.
                _context.Refreshtoken.UpdateRange(tokensToRevoke);

                // Saves changes to the database.
                int rowsAffected = await _context.SaveChangesAsync();

                // Check if the number of affected rows matches the expected count.
                if (rowsAffected >= tokensToRevoke.Count)
                {
                    _logger.LogInformation("Successfully revoked {Count} refresh tokens for session ID {SessionId}.", rowsAffected, sessionId);
                    return Result<bool>.Success(true);
                }
                else
                {
                    _logger.LogWarning("Revoked {RowsAffected} out of {ExpectedCount} tokens for session ID {SessionId}. Potential issue.", rowsAffected, tokensToRevoke.Count, sessionId);
                    return Result<bool>.Failure("Failed to revoke all expected tokens for session.");
                }
            }
            catch (DbUpdateException dbEx) // Handle database update errors.
            {
                _logger.LogError($"Database error revoking tokens by session ID {sessionId}: {dbEx.InnerException?.Message ?? dbEx.Message}");
                return Result<bool>.Failure("Database error revoking tokens by session ID.");
            }
            catch (Exception ex) // Handle general errors.
            {
                _logger.LogError("Error revoking tokens by session ID {SessionId}: {Message}", sessionId, ex.Message);
                return Result<bool>.Failure("Error revoking tokens by session ID.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<RefreshTokenEntity>> GetByTokenAsync(string token)
        {
            // Basic input validation.
            if (string.IsNullOrWhiteSpace(token))
                return Result<RefreshTokenEntity>.Failure("Refresh token cannot be empty.");
            try
            {
                // Finds the token by its string value, no tracking. Note comparison against DB 'Refreshtoken1'.
                var refreshToken = await _context.Refreshtoken
                                               .AsNoTracking()
                                               .FirstOrDefaultAsync(rt => rt.Refreshtoken1 == token);
                // Returns failure if not found.
                if (refreshToken == null)
                    return Result<RefreshTokenEntity>.Failure("Refresh token not found.");
                // Maps and returns success.
                return Result<RefreshTokenEntity>.Success(MapToDomainEntity(refreshToken));
            }
            catch (Exception ex)
            {
                _logger.LogError("Error retrieving refresh token by token value: {Error}", ex.Message); 
                return Result<RefreshTokenEntity>.Failure($"Error retrieving refresh token.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<IEnumerable<RefreshTokenEntity>>> GetActiveTokensByPersonIdAsync(int personId)
        {
            try
            {
                // Gets current time for expiration check.
                var now = _dateTimeProvider.GetUtcNow();
                // Finds tokens that belong to the user, are not revoked, and have not expired. No tracking.
                var activeTokens = await _context.Refreshtoken
                                               .AsNoTracking()
                                               .Where(rt => rt.Personid == personId && rt.Revokedat == null && rt.Expiresat > now) // Uses current time from provider
                                               .ToListAsync();
                // Maps results and returns success.
                return Result<IEnumerable<RefreshTokenEntity>>.Success(activeTokens.Select(MapToDomainEntity));
            }
            catch (Exception ex)
            {
                _logger.LogError("Error retrieving active refresh tokens for person ID {PersonId}: {Error}", personId, ex.Message); 
                return Result<IEnumerable<RefreshTokenEntity>>.Failure($"Error retrieving active refresh tokens for person ID.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<bool>> DeleteBySessionAsync(long sessionId)
        {
            try
            {
                // Finds all tokens belonging to the session ID.
                var tokensToDelete = await _context.Refreshtoken
                                                   .Where(rt => rt.Session == sessionId)
                                                   .ToListAsync();

                // If no tokens found, consider deletion successful.
                if (!tokensToDelete.Any()) return Result<bool>.Success(true);

                // Removes found tokens and saves changes.
                _context.Refreshtoken.RemoveRange(tokensToDelete);
                int rows = await _context.SaveChangesAsync();
                // Check if all intended tokens were deleted.
                return rows >= tokensToDelete.Count ? Result<bool>.Success(true) : Result<bool>.Failure("Failed to delete all tokens for session.");
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError("DB error deleting tokens by session: {DbError}", dbEx.InnerException?.Message ?? dbEx.Message);
                return Result<bool>.Failure("DB error deleting tokens by session.");
            }
            catch (Exception ex)
            {
                _logger.LogError("Error deleting tokens by session {SessionId}: {Error}", sessionId, ex.Message); 
                return Result<bool>.Failure($"Error deleting tokens by session.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<bool>> DeleteExpiredTokensAsync(int personId, DateTime olderThan)
        {
            try
            {
                // Uses the provided cutoff timestamp.
                var expiryDate = olderThan;
                // Finds tokens for the user that expired before the cutoff date.
                var tokensToDelete = await _context.Refreshtoken
                                                   .Where(rt => rt.Personid == personId && rt.Expiresat < expiryDate)
                                                   .ToListAsync();

                // If no expired tokens found, consider successful.
                if (!tokensToDelete.Any()) return Result<bool>.Success(true);

                // Removes found tokens and saves changes.
                _context.Refreshtoken.RemoveRange(tokensToDelete);
                int rows = await _context.SaveChangesAsync();
                // Check if all intended tokens were deleted.
                return rows >= tokensToDelete.Count ? Result<bool>.Success(true) : Result<bool>.Failure($"Failed to delete all expired tokens.");
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError("DB error deleting expired tokens: {DbError}", dbEx.InnerException?.Message ?? dbEx.Message); 
                return Result<bool>.Failure("DB error deleting expired tokens.");
            }
            catch (Exception ex)
            {
                _logger.LogError("Error deleting expired tokens for person ID {PersonId}: {Error}", personId, ex.Message);
                return Result<bool>.Failure("Error deleting expired tokens for person ID.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<RefreshTokenEntity>> GetById(int id)
        {
            try
            {
                // Finds token by PK, no tracking.
                var token = await _context.Refreshtoken.AsNoTracking().FirstOrDefaultAsync(t => t.Idrefreshtoken == id);
                // Returns failure if not found.
                if (token == null) return Result<RefreshTokenEntity>.Failure($"Refresh token with ID not found.");
                // Maps and returns success.
                return Result<RefreshTokenEntity>.Success(MapToDomainEntity(token));
            }
            catch (Exception ex)
            {
                _logger.LogError("Error retrieving refresh token by ID {Id}: {Error}", id, ex.Message); 
                return Result<RefreshTokenEntity>.Failure("Error retrieving refresh token by ID.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<IEnumerable<RefreshTokenEntity>>> GetAll()
        {
            try
            {
                // Retrieves all tokens, no tracking.
                var tokens = await _context.Refreshtoken.AsNoTracking().ToListAsync();
                // Maps list and returns success.
                return Result<IEnumerable<RefreshTokenEntity>>.Success(tokens.Select(MapToDomainEntity));
            }
            catch (Exception ex)
            {
                _logger.LogError("Error retrieving all refresh tokens: {Error}", ex.Message); 
                return Result<IEnumerable<RefreshTokenEntity>>.Failure($"Error retrieving all refresh tokens.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<RefreshTokenEntity>> Create(RefreshTokenEntity entity)
        {
            // Basic null check.
            if (entity == null) return Result<RefreshTokenEntity>.Failure("Refresh token entity cannot be null.");
            try
            {
                // Maps to DB model.
                var dbModel = MapToDbModel(entity);
                // Sets CreatedAt if not already set.
                if (dbModel.Createdat == default) dbModel.Createdat = _dateTimeProvider.GetUtcNow();

                // Adds to context and saves.
                await _context.Refreshtoken.AddAsync(dbModel);
                await _context.SaveChangesAsync();

                // --- Workaround: Update domain entity ID post-save ---
                var idProperty = typeof(RefreshTokenEntity).GetProperty(nameof(RefreshTokenEntity.IdRefreshToken));
                if (idProperty?.CanWrite ?? false)
                {
                    idProperty.SetValue(entity, dbModel.Idrefreshtoken, null);
                }
                else
                {
                    _logger.LogWarning("Could not set IdRefreshToken on domain entity after creation.");
                }
                // --- End Workaround ---

                // Returns success with potentially updated domain entity.
                return Result<RefreshTokenEntity>.Success(entity);
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError("DB error creating refresh token: {DbError}", dbEx.InnerException?.Message ?? dbEx.Message); 
                return Result<RefreshTokenEntity>.Failure("DB error creating refresh token.");
            }
            catch (Exception ex)
            {
                _logger.LogError("Error creating refresh token: {Error}", ex.Message); 
                return Result<RefreshTokenEntity>.Failure($"Error creating refresh token.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<bool>> Update(RefreshTokenEntity entity)
        {
            // Basic null check.
            if (entity == null) return Result<bool>.Failure("Refresh token entity cannot be null.");
            try
            {
                // Finds existing entity (tracked).
                var existing = await _context.Refreshtoken.FindAsync(entity.IdRefreshToken);
                // Returns failure if not found.
                if (existing == null) return Result<bool>.Failure($"Refresh token not found for update.");

                // Maps domain entity onto existing tracked entity.
                MapToDbModel(entity, existing);

                // Marks for update and saves.
                _context.Refreshtoken.Update(existing);
                int rows = await _context.SaveChangesAsync();
                // Returns success based on rows affected.
                return rows > 0 ? Result<bool>.Success(true) : Result<bool>.Failure("No changes were saved for the refresh token.");
            }
            catch (DbUpdateConcurrencyException ex) // Handles concurrency conflicts.
            {
                _logger.LogError(ex, "Concurrency error updating refresh token ID {IdRefreshToken}.", entity.IdRefreshToken); 
                return Result<bool>.Failure("Concurrency error updating refresh token.");
            }
            catch (DbUpdateException dbEx) // Handles other DB update errors.
            {
                _logger.LogError("DB error updating refresh token: {DbError}", dbEx.InnerException?.Message ?? dbEx.Message); 
                return Result<bool>.Failure("DB error updating refresh token");
            }
            catch (Exception ex) // Handles general errors.
            {
                _logger.LogError(ex, "Error updating refresh token: {Error}", ex.Message); 
                return Result<bool>.Failure("Error updating refresh token.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<bool>> Delete(int id)
        {
            try
            {
                // Finds entity by ID (tracked).
                var token = await _context.Refreshtoken.FindAsync(id);
                // Returns failure if not found.
                if (token == null) return Result<bool>.Failure($"Refresh token not found for deletion.");

                // Removes from context and saves.
                _context.Refreshtoken.Remove(token);
                int rows = await _context.SaveChangesAsync();
                // Returns success based on rows affected.
                return rows > 0 ? Result<bool>.Success(true) : Result<bool>.Failure("Failed to delete the refresh token (no rows affected).");
            }
            catch (DbUpdateException dbEx) // Handles DB deletion errors.
            {
                _logger.LogError("DB error deleting refresh token: {DbError}", dbEx.InnerException?.Message ?? dbEx.Message); 
                return Result<bool>.Failure($"DB error deleting refresh token.");
            }
            catch (Exception ex) // Handles general errors.
            {
                _logger.LogError("Error deleting refresh token: {Error}", ex.Message); 
                return Result<bool>.Failure($"Error deleting refresh token.");
            }
        }
    }
}