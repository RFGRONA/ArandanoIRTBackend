using ArandanoIRT_Backend.Application.Interfaces.Utilities;
using ArandanoIRT_Backend.Domain.Entities;
using ArandanoIRT_Backend.Domain.IRepositories;
using ArandanoIRT_Backend.Domain.ValueObjects;
using ArandanoIRT_Backend.Infrastructure.Data; 
using Microsoft.EntityFrameworkCore;

namespace ArandanoIRT_Backend.Infrastructure.Repositories
{
    /// <summary>
    /// Implements the <see cref="IPersonRepository"/> interface, providing data access logic
    /// for person (user) entities (<see cref="PersonEntity"/>) using Entity Framework Core.
    /// </summary>
    public class PersonRepository(ApplicationDbContext context, IDateTimeProvider dateTimeProvider, ILogger<PersonRepository> logger) : IPersonRepository
    {
        private readonly ApplicationDbContext _context = context ?? throw new ArgumentNullException(nameof(context));
        private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
        private readonly ILogger<PersonRepository> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // --- Mapping Methods ---
        private static PersonEntity? MapToDomainEntity(Person? person) // Allow nullable input
        {
            if (person == null) return null;

            var personEntity = new PersonEntity(
                person.Idperson,
                person.Firstname,
                person.Lastname,
                person.Email,
                person.Password, // Include password from DB when mapping back
                person.Createdat,
                person.Isadmin,
                person.Allnotifications,
                person.Cropid
            );

            // Set nullable timestamps using reflection (consider adding to constructor if feasible)
            SetPropertyValue(personEntity, nameof(PersonEntity.UpdatedAt), person.Updatedat);
            SetPropertyValue(personEntity, nameof(PersonEntity.LastLoginAt), person.Lastloginat);
            SetPropertyValue(personEntity, nameof(PersonEntity.LastPasswordChangeAt), person.Lastpasswordchangeat);

            return personEntity;
        }

        // Helper for reflection to reduce repetition
        private static void SetPropertyValue(object target, string propertyName, object? value)
        {
            try
            {
                var property = target.GetType().GetProperty(propertyName);
                if (property?.CanWrite == true)
                {
                    property.SetValue(target, value, null);
                }
            }
            catch (Exception ex)
            {
                // Consider logging this if it's critical, but avoid throwing from a mapper
                Console.WriteLine($"Warning: Could not set property {propertyName} via reflection during mapping. Error: {ex.Message}");
                // Log.Warning(...) if a logger were available here (static method limitation)
            }
        }


        private static Person MapToDbModel(PersonEntity entity, Person? existingPerson = null)
        {
            var person = existingPerson ?? new Person();

            // ID should only be mapped if existingPerson is provided (for update context)
            if (existingPerson != null)
            {
                person.Idperson = entity.IdPerson;
            }

            person.Firstname = entity.FirstName;
            person.Lastname = entity.LastName;
            person.Email = entity.Email.ToLowerInvariant(); // Normalize email
            person.Isadmin = entity.IsAdmin;
            person.Allnotifications = entity.AllNotifications;
            person.Cropid = entity.CropId;
            person.Createdat = entity.CreatedAt;
            person.Updatedat = entity.UpdatedAt;
            person.Lastloginat = entity.LastLoginAt;
            person.Lastpasswordchangeat = entity.LastPasswordChangeAt;

            // *** IMPORTANT: Password is NOT mapped on update via this method ***
            // Only set password if creating NEW record AND password is provided
            if (existingPerson == null && !string.IsNullOrWhiteSpace(entity.Password))
            {
                person.Password = entity.Password; // Assumes hash is provided
            }

            return person;
        }

        // --- Repository Methods ---

        /// <inheritdoc/>
        public async Task<Result<bool>> UpdatePasswordAsync(int personId, string newPasswordHash, DateTime changeTimestamp)
        {
            if (string.IsNullOrWhiteSpace(newPasswordHash))
                return Result<bool>.Failure("New password hash cannot be empty.");

            try
            {
                // Use FindAsync for potential tracking benefits if needed elsewhere, else FirstOrDefault is fine.
                var person = await _context.Person.FindAsync(personId); // Track the entity

                if (person == null)
                    return Result<bool>.Failure("Person not found for password update.");

                // Update relevant fields directly
                person.Password = newPasswordHash;
                person.Lastpasswordchangeat = changeTimestamp;
                person.Updatedat = changeTimestamp; // Also update general timestamp

                int rowsAffected = await _context.SaveChangesAsync();

                return rowsAffected > 0
                    ? Result<bool>.Success(true)
                    : Result<bool>.Failure("No changes were saved for the person's password.");
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex, "Concurrency conflict updating password for person ID {IdPerson}.", personId);
                return Result<bool>.Failure("Concurrency conflict updating password.");
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error updating password for person {IdPerson}: {Message}", personId, dbEx.InnerException?.Message ?? dbEx.Message);
                return Result<bool>.Failure("Database error updating password.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating password for person {IdPerson}: {Message}", personId, ex.Message);
                return Result<bool>.Failure("Error updating password.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<PersonEntity>> GetById(int id)
        {
            try
            {
                var person = await _context.Person
                                           .AsNoTracking()
                                           .FirstOrDefaultAsync(p => p.Idperson == id);

                var domainEntity = MapToDomainEntity(person);
                if (domainEntity == null) return Result<PersonEntity>.Failure("Person not found.");

                return Result<PersonEntity>.Success(domainEntity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving person by ID: {IdPerson}", id);
                return Result<PersonEntity>.Failure("Error retrieving person by ID.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<PersonEntity>> GetByEmailAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return Result<PersonEntity>.Failure("Email cannot be empty.");

            try
            {
                var normalizedEmail = email.ToLowerInvariant();
                var person = await _context.Person
                                           .AsNoTracking()
                                           .FirstOrDefaultAsync(p => p.Email.ToLower() == normalizedEmail);

                var domainEntity = MapToDomainEntity(person);
                if (domainEntity == null) return Result<PersonEntity>.Failure("Person not found.");

                // Password hash is included via MapToDomainEntity when getting by email
                return Result<PersonEntity>.Success(domainEntity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving person by email: {Message}", ex.Message);
                return Result<PersonEntity>.Failure("Error retrieving person by email.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<IEnumerable<PersonEntity>>> GetAll()
        {
            try
            {
                var people = await _context.Person
                                           .AsNoTracking()
                                           .ToListAsync();

                // Use OfType<PersonEntity> to safely handle potential nulls from MapToDomainEntity
                return Result<IEnumerable<PersonEntity>>.Success(people.Select(MapToDomainEntity).OfType<PersonEntity>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all people: {Message}", ex.Message);
                return Result<IEnumerable<PersonEntity>>.Failure("Error retrieving all people.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<PersonEntity>> Create(PersonEntity entity)
        {
            if (entity == null)
                return Result<PersonEntity>.Failure("Person entity cannot be null.");

            try
            {
                // Check email existence first
                var normalizedEmailToCheck = entity.Email.ToLowerInvariant();
                bool emailExists = await _context.Person.AnyAsync(p => p.Email.ToLower() == normalizedEmailToCheck);
                if (emailExists)
                {
                    return Result<PersonEntity>.Failure("Email is already registered.");
                }

                // Map and set defaults
                var personDbModel = MapToDbModel(entity);
                if (personDbModel.Createdat == default)
                    personDbModel.Createdat = _dateTimeProvider.GetUtcNow();
                // Ensure other timestamps are null
                personDbModel.Updatedat = null;
                personDbModel.Lastloginat = null;
                personDbModel.Lastpasswordchangeat = null;

                // Add and save
                await _context.Person.AddAsync(personDbModel);
                int success = await _context.SaveChangesAsync();

                if (success == 0)
                {
                    _logger.LogWarning("Failed to save new person to the database. Entity: {@PersonEntity}", entity);
                    return Result<PersonEntity>.Failure("Failed to save person to the database.");
                }

                // Map saved DB model (with ID) back to a new domain entity
                var createdEntity = MapToDomainEntity(personDbModel);
                if (createdEntity == null) // Defensive check
                {
                    _logger.LogError("Failed to map newly created person back to domain entity. DB ID: {DbPersonId}", personDbModel.Idperson);
                    return Result<PersonEntity>.Failure("Failed to map created person.");
                }

                _logger.LogInformation("Successfully created person with ID {PersonId}.", createdEntity.IdPerson);
                return Result<PersonEntity>.Success(createdEntity); // Return newly mapped entity
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error creating person: {Message}", dbEx.InnerException?.Message ?? dbEx.Message);
                return Result<PersonEntity>.Failure("Database error creating person.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating person: {Message}", ex.Message);
                return Result<PersonEntity>.Failure("Error creating person.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<bool>> Update(PersonEntity entity)
        {
            if (entity == null)
                return Result<bool>.Failure("Person entity cannot be null.");
            if (entity.IdPerson <= 0) // Added valid ID check
                return Result<bool>.Failure("Invalid Person ID provided for update.");

            try
            {
                // Call helper to perform find, validation, update, save
                return await FindValidateUpdateAndSaveAsync(entity);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex, "Concurrency conflict updating person ID {IdPerson}.", entity.IdPerson);
                return Result<bool>.Failure("Concurrency conflict updating person.");
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error updating person ID {IdPerson}: {Message}", entity.IdPerson, dbEx.InnerException?.Message ?? dbEx.Message);
                return Result<bool>.Failure("Database error updating person.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating person ID {IdPerson}: {Message}", entity.IdPerson, ex.Message);
                return Result<bool>.Failure("Error updating person.");
            }
        }


        /// <summary>
        /// Finds the existing person, checks email uniqueness if changed, maps updates, sets timestamp, saves, and returns result.
        /// </summary>
        /// <param name="entity">The domain entity with updated data.</param>
        /// <returns>Result indicating success or failure of the update.</returns>
        /// <exception cref="DbUpdateConcurrencyException">Propagated concurrency exception.</exception>
        /// <exception cref="DbUpdateException">Propagated database update exception.</exception>
        private async Task<Result<bool>> FindValidateUpdateAndSaveAsync(PersonEntity entity)
        {
            // Find existing entity
            var existingPerson = await _context.Person.FindAsync(entity.IdPerson);
            if (existingPerson == null)
                return Result<bool>.Failure("Person not found for update.");

            // Check email uniqueness if it changed
            var emailCheckResult = await CheckEmailUniquenessOnUpdateAsync(existingPerson, entity);
            if (emailCheckResult.IsFailure)
                return Result<bool>.Failure(emailCheckResult.ErrorMessage); // Return failure from check

            // Map domain entity onto tracked DB entity (does not map password)
            MapToDbModel(entity, existingPerson);

            // Set UpdatedAt timestamp
            SetUpdatedAt(existingPerson);

            // Mark and Save
            _context.Person.Update(existingPerson);
            int rowsAffected = await _context.SaveChangesAsync();

            // Return result
            if (rowsAffected > 0)
            {
                _logger.LogInformation("Successfully updated person ID: {PersonId}", entity.IdPerson);
                return Result<bool>.Success(true);
            }
            else
            {
                _logger.LogInformation("No changes were detected or saved for person ID: {PersonId}.", entity.IdPerson);
                // Return Success(false) as no error occurred, but no change was saved.
                return Result<bool>.Success(false);
            }
        }


        /// <summary>
        /// Checks if the email in the updated entity, if different from the existing one, is already used by another person.
        /// </summary>
        /// <param name="existingPerson">The current person entity from the database.</param>
        /// <param name="updatedEntity">The incoming domain entity with potential changes.</param>
        /// <returns>A Success Result if the email is unique or unchanged, Failure otherwise.</returns>
        private async Task<Result<bool>> CheckEmailUniquenessOnUpdateAsync(Person existingPerson, PersonEntity updatedEntity)
        {
            if (!string.Equals(existingPerson.Email, updatedEntity.Email, StringComparison.OrdinalIgnoreCase))
            {
                var normalizedNewEmail = updatedEntity.Email.ToLowerInvariant();
                bool emailExists = await _context.Person.AnyAsync(p => p.Email.ToLower() == normalizedNewEmail && p.Idperson != updatedEntity.IdPerson);
                if (emailExists)
                {
                    _logger.LogWarning("Attempted to update Person ID {PersonId} with email {Email} which is already in use.", updatedEntity.IdPerson, updatedEntity.Email);
                    return Result<bool>.Failure("Cannot update email, is already in use by another user.");
                }
            }
            return Result<bool>.Success(true); // Email is unchanged or is unique
        }

        /// <summary>
        /// Sets the UpdatedAt timestamp on the Person database entity to the current UTC time.
        /// </summary>
        /// <param name="existingPerson">The database entity being updated.</param>
        private void SetUpdatedAt(Person existingPerson)
        {
            existingPerson.Updatedat = _dateTimeProvider.GetUtcNow();
        }


        /// <inheritdoc/>
        public async Task<Result<bool>> Delete(int id)
        {
            try
            {
                var person = await _context.Person.FindAsync(id);
                if (person == null)
                    return Result<bool>.Failure("Person not found for deletion.");

                _context.Person.Remove(person);
                int rowsAffected = await _context.SaveChangesAsync();

                if (rowsAffected > 0)
                {
                    _logger.LogInformation("Successfully deleted person ID: {PersonId}", id);
                    return Result<bool>.Success(true);
                }
                else
                {
                    _logger.LogWarning("Failed to delete person (no rows affected) ID: {PersonId}", id);
                    return Result<bool>.Failure("Failed to delete the person.");
                }
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error deleting person ID {IdPerson} (check related records): {Message}", id, dbEx.InnerException?.Message ?? dbEx.Message);
                return Result<bool>.Failure("Database error deleting person (check related records).");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting person ID {IdPerson}: {Message}", id, ex.Message);
                return Result<bool>.Failure("Error deleting person.");
            }
        }
    }
}