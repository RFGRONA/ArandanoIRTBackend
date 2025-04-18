using ArandanoIRT_Backend.Application.Interfaces.Utilities;
using ArandanoIRT_Backend.Domain.Entities;
using ArandanoIRT_Backend.Domain.IRepositories;
using ArandanoIRT_Backend.Domain.ValueObjects;
using ArandanoIRT_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ArandanoIRT_Backend.Infrastructure.Repositories
{
    /// <summary>
    /// Implements the <see cref="IChangePasswordRepository"/> interface, providing data access logic
    /// for password reset token entities (<see cref="ChangePasswordEntity"/>) using Entity Framework Core.
    /// </summary>
    public class ChangePasswordRepository(ApplicationDbContext context, IDateTimeProvider dateTimeProvider, ILogger<ChangePasswordRepository> logger) : IChangePasswordRepository
    {
        private readonly ApplicationDbContext _context = context ?? throw new ArgumentNullException(nameof(context));
        private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
        /// <summary>
        /// Logger instance for logging repository operations and errors.
        /// </summary>
        private readonly ILogger<ChangePasswordRepository> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        /// <summary>
        /// Maps a database context <see cref="Changepassword"/> entity to a domain <see cref="ChangePasswordEntity"/>.
        /// </summary>
        /// <param name="token">The database entity instance.</param>
        /// <returns>The mapped domain entity instance, or <c>null</c> if the input is null (throws NullReferenceException due to null! usage if input is null).</returns>
        /// <remarks>
        /// Note: Handles potential null Personid from DB by defaulting to 0 in the domain entity.
        /// </remarks>
        private static ChangePasswordEntity MapToDomainEntity(Changepassword token)
        {
            // Guards against null input, though using null forgiving operator suggests it shouldn't be null in expected flows.
            if (token == null) return null!; // Consider throwing ArgumentNullException if null is truly unexpected.
            return new ChangePasswordEntity(
                token.Idchangepassword,
                token.Passwordresettoken,
                token.Resettokenexpiresat,
                token.Tokencreatedat,
                token.Personid 
            );
        }

        /// <summary>
        /// Maps a domain <see cref="ChangePasswordEntity"/> to a database context <see cref="Changepassword"/> entity.
        /// If an existing database entity is provided, it updates its properties; otherwise, it creates a new one.
        /// </summary>
        /// <param name="entity">The domain entity instance.</param>
        /// <param name="existingToken">Optional. The existing database entity to update.</param>
        /// <returns>The mapped or updated database entity instance.</returns>
        private static Changepassword MapToDbModel(ChangePasswordEntity entity, Changepassword? existingToken = null)
        {
            // Uses existing instance or creates a new one.
            var token = existingToken ?? new Changepassword();
            // Maps properties from domain entity to database model.
            token.Idchangepassword = entity.IdChangePassword; // Note: Usually ID is not set manually unless updating.
            token.Passwordresettoken = entity.PasswordResetToken;
            token.Resettokenexpiresat = entity.ResetTokenExpiresAt;
            token.Tokencreatedat = entity.TokenCreatedAt;
            token.Personid = entity.PersonId; // Maps domain int to DB nullable int?.
            return token;
        }

        /// <inheritdoc/>
        public async Task<Result<ChangePasswordEntity>> GetActiveByTokenAsync(string token)
        {
            // Basic input validation.
            if (string.IsNullOrWhiteSpace(token))
                return Result<ChangePasswordEntity>.Failure("Reset token cannot be empty.");
            try
            {
                // Gets the current UTC time.
                var now = _dateTimeProvider.GetUtcNow();
                // Queries for the token, ensuring it hasn't expired. Uses AsNoTracking for read-only operation.
                var resetToken = await _context.Changepassword
                                               .AsNoTracking()
                                               .FirstOrDefaultAsync(t => t.Passwordresettoken == token && t.Resettokenexpiresat > now); // Uses current time from provider

                // Checks if the token was found and is active.
                if (resetToken == null)
                    return Result<ChangePasswordEntity>.Failure("Password reset token is invalid or expired.");

                // Maps the database entity to the domain entity and returns success.
                return Result<ChangePasswordEntity>.Success(MapToDomainEntity(resetToken));
            }
            catch (Exception ex)
            {
                // Logs any unexpected error during retrieval.
                _logger.LogError(ex, $"Error retrieving reset token by token value: {ex.Message}");
                return Result<ChangePasswordEntity>.Failure("Error retrieving reset token.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<bool>> DeleteTokensByPersonIdAsync(int personId)
        {
            try
            {
                // Finds all tokens associated with the given person ID.
                var tokensToDelete = await _context.Changepassword
                                                   .Where(t => t.Personid == personId)
                                                   .ToListAsync();

                // If no tokens are found, operation is considered successful.
                if (!tokensToDelete.Any())
                    return Result<bool>.Success(true);

                // Removes the found tokens from the context.
                _context.Changepassword.RemoveRange(tokensToDelete);
                // Saves changes to the database.
                int rowsAffected = await _context.SaveChangesAsync();

                // Verifies if the number of affected rows matches the number of tokens intended for deletion.
                return rowsAffected >= tokensToDelete.Count
                    ? Result<bool>.Success(true)
                    : Result<bool>.Failure("Failed to delete all reset tokens for person.");
            }
            catch (DbUpdateException dbEx)
            {
                // Logs database-specific update errors.
                _logger.LogError(dbEx, $"Database error deleting reset tokens for PersonId {personId}, {dbEx.InnerException?.Message ?? dbEx.Message}.");
                return Result<bool>.Failure("Database error deleting reset tokens.");
            }
            catch (Exception ex)
            {
                // Logs general errors during the deletion process.
                _logger.LogError(ex, $"Error deleting reset tokens for PersonId {personId}: {ex.Message}");
                return Result<bool>.Failure("Error deleting reset tokens for person.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<ChangePasswordEntity>> GetById(int id)
        {
            try
            {
                // Finds a token by its primary key using FindAsync (if tracked) or FirstOrDefaultAsync. Using AsNoTracking.
                var token = await _context.Changepassword.AsNoTracking().FirstOrDefaultAsync(t => t.Idchangepassword == id);
                // Returns failure if not found.
                if (token == null) return Result<ChangePasswordEntity>.Failure("Reset token not found.");
                // Maps and returns success if found.
                return Result<ChangePasswordEntity>.Success(MapToDomainEntity(token));
            }
            catch (Exception ex)
            {
                // Logs errors during retrieval.
                _logger.LogError(ex, $"Error retrieving reset token by ID {id}: {ex.Message}"); 
                return Result<ChangePasswordEntity>.Failure("Error retrieving reset token.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<IEnumerable<ChangePasswordEntity>>> GetAll()
        {
            try
            {
                // Retrieves all tokens using AsNoTracking.
                var tokens = await _context.Changepassword.AsNoTracking().ToListAsync();
                // Maps the list of database entities to domain entities and returns success.
                return Result<IEnumerable<ChangePasswordEntity>>.Success(tokens.Select(MapToDomainEntity));
            }
            catch (Exception ex)
            {
                // Logs errors during retrieval.
                _logger.LogError(ex, $"Error retrieving all reset tokens: {ex.Message}");
                return Result<IEnumerable<ChangePasswordEntity>>.Failure("Error retrieving all reset tokens.");
            }
        }

        /// <inheritdoc/>
        /// <inheritdoc/>
        /// <remarks>
        /// This implementation ensures atomicity by wrapping the deletion of existing tokens
        /// for the user and the creation of the new token within a single database transaction.
        /// It first marks any existing tokens for deletion, adds the new token, and then saves
        /// all changes together. If any step fails, the transaction is rolled back.
        /// After successful creation, it attempts to update the input domain entity's ID
        /// with the value generated by the database using reflection.
        /// </remarks>
        public async Task<Result<ChangePasswordEntity>> Create(ChangePasswordEntity entity)
        {
            // Basic null check for the input domain entity.
            if (entity == null)
                return Result<ChangePasswordEntity>.Failure("Reset token entity cannot be null.");

            // Begins a database transaction to ensure atomicity.
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Find existing tokens for the specified person within the transaction.
                var tokensToDelete = await _context.Changepassword
                                                  .Where(t => t.Personid == entity.PersonId)
                                                  .ToListAsync(); // Materialize the list to avoid issues during RemoveRange.

                // 2. Mark any existing tokens for deletion.
                if (tokensToDelete.Any())
                {
                    _context.Changepassword.RemoveRange(tokensToDelete);
                    _logger.LogInformation("Marked {Count} existing reset tokens for deletion for PersonId {PersonId}.", tokensToDelete.Count, entity.PersonId);
                }

                // 3. Map the input domain entity to the database model.
                var dbModel = MapToDbModel(entity);
                // Sets the creation timestamp if it's not already set (using default value check).
                if (dbModel.Tokencreatedat == default)
                {
                    dbModel.Tokencreatedat = _dateTimeProvider.GetUtcNow();
                }

                // 4. Add the new token model to the DbContext.
                await _context.Changepassword.AddAsync(dbModel);
                _logger.LogInformation("Marked new reset token for addition for PersonId {PersonId}.", entity.PersonId);

                // 5. Save all tracked changes (deletions and the new addition) atomically.
                int affectedRows = await _context.SaveChangesAsync();
                _logger.LogDebug("SaveChangesAsync completed within Create reset token transaction. Rows affected: {RowsAffected}", affectedRows);

                // Optional check: Ensure at least the insert happened.
                if (affectedRows == 0 && !tokensToDelete.Any()) // If nothing was deleted and nothing was inserted
                {
                    _logger.LogWarning("SaveChangesAsync reported 0 rows affected while creating reset token for PersonId {PersonId}, and no prior tokens were deleted.", entity.PersonId);
                    // Rollback as the intended operation likely failed silently.
                    await transaction.RollbackAsync();
                    return Result<ChangePasswordEntity>.Failure("Failed to save new reset token.");
                }

                // 6. Commit the transaction as all operations succeeded.
                await transaction.CommitAsync();
                _logger.LogInformation("Transaction committed for Create reset token for PersonId {PersonId}.", entity.PersonId);

                // --- Workaround: Update domain entity ID post-save ---
                // Reflects the database-generated ID back onto the input domain entity.
                var idProperty = typeof(ChangePasswordEntity).GetProperty(nameof(ChangePasswordEntity.IdChangePassword));
                if (idProperty?.CanWrite == true)
                {
                    idProperty.SetValue(entity, dbModel.Idchangepassword, null);
                }
                else
                {
                    // Logs a warning if the ID could not be set back on the domain entity.
                    _logger.LogWarning("Could not set IdChangePassword on domain entity after creation for PersonId {PersonId}.", entity.PersonId);
                    // The returned 'entity' might have an incorrect ID (0) in this edge case.
                }
                // --- End Workaround ---

                // Returns success with the potentially updated domain entity.
                return Result<ChangePasswordEntity>.Success(entity);
            }
            catch (Exception ex) // Catches any exception during the transaction.
            {
                // If any error occurs, attempts to rollback the transaction.
                _logger.LogError(ex, "Error occurred during Create reset token transaction for PersonId {PersonId}. Rolling back.", entity.PersonId);
                try
                {
                    await transaction.RollbackAsync();
                }
                catch (Exception rbEx)
                {
                    _logger.LogError(rbEx, "Error occurred during transaction rollback for PersonId {PersonId}.", entity.PersonId);
                    // Log the rollback error, but the original exception is more relevant to return.
                }


                // Logs and returns the specific error type and message.
                if (ex is DbUpdateException dbEx)
                {
                    // Log message remains Spanish in code.
                    _logger.LogError(dbEx, "DB transaction error creating reset token: {DbError}", dbEx.InnerException?.Message ?? dbEx.Message);
                    return Result<ChangePasswordEntity>.Failure("DB transaction error creating reset token.");
                }

                // Logs any other unexpected error.
                _logger.LogError(ex, "Transaction error creating reset token: {Error}", ex.Message);
                return Result<ChangePasswordEntity>.Failure("Transaction error creating reset token.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<bool>> Update(ChangePasswordEntity entity)
        {
            // Basic null check for the input domain entity.
            if (entity == null) return Result<bool>.Failure("Reset token entity cannot be null.");
            try
            {
                // Finds the existing database entity by ID. Uses FindAsync which tracks the entity.
                var existing = await _context.Changepassword.FindAsync(entity.IdChangePassword);
                // Returns failure if the entity to update is not found.
                if (existing == null) return Result<bool>.Failure("Reset token not found for update.");

                // Maps properties from the domain entity onto the tracked database entity.
                MapToDbModel(entity, existing);

                // Marks the entity as modified (though FindAsync might already track changes if properties were set directly).
                // Explicitly calling Update ensures the state is Modified.
                _context.Changepassword.Update(existing);
                // Saves changes to the database.
                int rows = await _context.SaveChangesAsync();
                // Returns success if at least one row was affected, failure otherwise.
                return rows > 0 ? Result<bool>.Success(true) : Result<bool>.Failure("No changes were saved for the reset token update.");
            }
            catch (DbUpdateConcurrencyException ex) // Handles concurrency conflicts.
            {
                _logger.LogError(ex, "Concurrency error updating reset token ID {Id}.", entity.IdChangePassword);  
                return Result<bool>.Failure("Concurrency error updating reset token.");
            }
            catch (DbUpdateException dbEx) // Handles other database update errors.
            {
                _logger.LogError(dbEx, "DB error updating reset token: {DbError}", dbEx.InnerException?.Message ?? dbEx.Message); 
                return Result<bool>.Failure("DB error updating reset token.");
            }
            catch (Exception ex) // Handles general errors.
            {
                _logger.LogError(ex, "Error updating reset token: {Error}", ex.Message); 
                return Result<bool>.Failure("Error updating reset token.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<bool>> Delete(int id)
        {
            try
            {
                // Finds the entity by ID to delete. Uses FindAsync which tracks the entity.
                var token = await _context.Changepassword.FindAsync(id);
                // Returns failure if the entity is not found.
                if (token == null) return Result<bool>.Failure($"Reset token ID {id} not found for deletion.");

                // Removes the entity from the context.
                _context.Changepassword.Remove(token);
                // Saves changes to the database.
                int rows = await _context.SaveChangesAsync();
                // Returns success if at least one row was affected, failure otherwise.
                return rows > 0 ? Result<bool>.Success(true) : Result<bool>.Failure("Failed to delete reset token (no rows affected).");
            }
            catch (DbUpdateException dbEx) // Handles database deletion errors.
            {
                _logger.LogError(dbEx, "DB error deleting reset token: {DbError}", dbEx.InnerException?.Message ?? dbEx.Message);
                return Result<bool>.Failure("DB error deleting reset token.");
            }
            catch (Exception ex) // Handles general errors.
            {
                _logger.LogError(ex, "Error deleting reset token: {Error}", ex.Message); 
                return Result<bool>.Failure("Error deleting reset token.");
            }
        }
    }
}