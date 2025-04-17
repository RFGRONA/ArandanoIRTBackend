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
        /// <summary>
        /// The database context used for data access.
        /// </summary>
        private readonly ApplicationDbContext _context = context ?? throw new ArgumentNullException(nameof(context));
        /// <summary>
        /// Provider for obtaining consistent UTC timestamps.
        /// </summary>
        private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
        /// <summary>
        /// Logger instance for logging repository operations and errors.
        /// </summary>
        private readonly ILogger<PersonRepository> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        /// <summary>
        /// Maps a database context <see cref="Person"/> entity to a domain <see cref="PersonEntity"/>.
        /// </summary>
        /// <param name="person">The database entity instance.</param>
        /// <returns>The mapped domain entity instance, or <c>null</c> if input is null (throws NullReferenceException due to null! usage).</returns>
        /// <remarks>
        /// This mapping intentionally passes <see cref="string.Empty"/> for the password to the domain entity constructor.
        /// Timestamps like UpdatedAt, LastLoginAt, LastPasswordChangeAt are set using reflection after construction.
        /// The actual password hash is *not* typically loaded into the domain entity by this read-mapping method, except potentially in GetByEmailAsync.
        /// </remarks>
        private static PersonEntity MapToDomainEntity(Person person)
        {
            // Guard against null input.
            if (person == null) return null!;

            // Create domain entity, passing empty string for password initially.
            var personEntity = new PersonEntity(
                person.Idperson,
                person.Firstname,
                person.Lastname,
                person.Email,
                person.Password,
                person.Createdat,
                person.Isadmin,
                person.Allnotifications,
                person.Cropid
            );

            // Use reflection to set nullable timestamp properties on the domain entity.
            var updatedAtProperty = typeof(PersonEntity).GetProperty(nameof(PersonEntity.UpdatedAt));
            if (updatedAtProperty?.CanWrite ?? false) updatedAtProperty.SetValue(personEntity, person.Updatedat, null);

            var lastLoginAtProperty = typeof(PersonEntity).GetProperty(nameof(PersonEntity.LastLoginAt));
            if (lastLoginAtProperty?.CanWrite ?? false) lastLoginAtProperty.SetValue(personEntity, person.Lastloginat, null);

            var lastPasswordChangeAtProperty = typeof(PersonEntity).GetProperty(nameof(PersonEntity.LastPasswordChangeAt));
            if (lastPasswordChangeAtProperty?.CanWrite ?? false) lastPasswordChangeAtProperty.SetValue(personEntity, person.Lastpasswordchangeat, null);

            return personEntity;
        }

        /// <summary>
        /// Maps a domain <see cref="PersonEntity"/> to a database context <see cref="Person"/> entity.
        /// Updates existing instance if provided, otherwise creates a new one.
        /// </summary>
        /// <param name="entity">The domain entity instance.</param>
        /// <param name="existingPerson">Optional. The existing database entity to update.</param>
        /// <returns>The mapped or updated database entity instance.</returns>
        /// <remarks>
        /// The password property is only mapped if creating a new record (<paramref name="existingPerson"/> is null)
        /// and the domain entity's password is not empty. Password updates should use <see cref="UpdatePasswordAsync"/>.
        /// Email is normalized to lowercase.
        /// </remarks>
        private static Person MapToDbModel(PersonEntity entity, Person? existingPerson = null)
        {
            // Use existing instance or create a new one.
            var person = existingPerson ?? new Person();

            // Map properties from domain entity to database model.
            person.Idperson = entity.IdPerson; // Usually ID is not set manually unless creating.
            person.Firstname = entity.FirstName;
            person.Lastname = entity.LastName;
            person.Email = entity.Email.ToLowerInvariant(); // Normalize email to lowercase.
            person.Isadmin = entity.IsAdmin;
            person.Allnotifications = entity.AllNotifications;
            person.Cropid = entity.CropId;
            person.Createdat = entity.CreatedAt; // Maps CreatedAt for potential updates if needed.
            person.Updatedat = entity.UpdatedAt; // Maps UpdatedAt.
            person.Lastloginat = entity.LastLoginAt; // Maps LastLoginAt.
            person.Lastpasswordchangeat = entity.LastPasswordChangeAt; // Maps LastPasswordChangeAt.

            // Only set the password hash if creating a new record AND password is provided in domain entity.
            if (!string.IsNullOrWhiteSpace(entity.Password) && existingPerson == null)
            {
                person.Password = entity.Password; // Assumes entity.Password contains the hash.
            }
            // For updates, password changes MUST go through UpdatePasswordAsync.

            return person;
        }

        /// <inheritdoc/>
        public async Task<Result<bool>> UpdatePasswordAsync(int personId, string newPasswordHash, DateTime changeTimestamp)
        {
            // Basic validation for the new hash.
            if (string.IsNullOrWhiteSpace(newPasswordHash))
            {
                return Result<bool>.Failure("New password hash cannot be empty.");
            }

            try
            {
                // Find the person *with tracking* to update directly.
                var person = await _context.Person.FirstOrDefaultAsync(p => p.Idperson == personId);

                // Return failure if person not found.
                if (person == null)
                {
                    return Result<bool>.Failure("Person not found for password update.");
                }

                // Update only the necessary fields directly on the tracked entity.
                person.Password = newPasswordHash;
                person.Lastpasswordchangeat = changeTimestamp;
                person.Updatedat = changeTimestamp; // Also update the general update timestamp.

                // Save changes to the database.
                int rowsAffected = await _context.SaveChangesAsync();

                // Returns success based on rows affected.
                return rowsAffected > 0
                    ? Result<bool>.Success(true)
                    : Result<bool>.Failure("No changes were saved for the person's password.");
            }
            catch (DbUpdateConcurrencyException ex) // Handle concurrency conflicts.
            {
                _logger.LogError(ex, "Concurrency conflict updating password for person with ID {IdPerson}.", personId); 
                return Result<bool>.Failure("Concurrency conflict updating password for person.");
            }
            catch (DbUpdateException dbEx) // Handle other DB update errors.
            {
                _logger.LogError(dbEx, "Database error updating password for person {IdPerson}: {Message}", personId, dbEx.InnerException?.Message ?? dbEx.Message);
                return Result<bool>.Failure("Database error updating password.");
            }
            catch (Exception ex) // Handle general errors.
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
                // Retrieves person by ID, no tracking.
                var person = await _context.Person
                                           .AsNoTracking()
                                           .FirstOrDefaultAsync(p => p.Idperson == id);

                // Returns failure if not found.
                if (person == null)
                    return Result<PersonEntity>.Failure("Person not found.");

                // Maps and returns success. (Password hash not included in domain entity here).
                return Result<PersonEntity>.Success(MapToDomainEntity(person));
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
            try
            {
                // Normalize email for case-insensitive comparison.
                var normalizedEmail = email?.ToLowerInvariant();
                // Retrieves person by normalized email, no tracking.
                var person = await _context.Person
                                           .AsNoTracking()
                                           .FirstOrDefaultAsync(p => p.Email.ToLower() == normalizedEmail);

                // Returns failure if not found.
                if (person == null)
                    return Result<PersonEntity>.Failure("Person not found.");

                // Maps the DB entity to the domain entity.
                var domainEntity = MapToDomainEntity(person);
                // Checks if mapping was successful before attempting to set password.
                if (domainEntity == null)
                {
                    _logger.LogError("Error mapping Person entity after retrieving it via email {Email}", email);
                    return Result<PersonEntity>.Failure("Internal error processing user data.");
                }

                // Returns success with the domain entity (now including the password hash).
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
                // Retrieves all people, no tracking.
                var people = await _context.Person
                                           .AsNoTracking()
                                           .ToListAsync();

                // Maps list and returns success. (Password hashes not included).
                return Result<IEnumerable<PersonEntity>>.Success(people.Select(MapToDomainEntity));
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
            // Basic null check.
            if (entity == null)
                return Result<PersonEntity>.Failure("Person entity cannot be null.");

            try
            {
                // Checks if email already exists (case-insensitive).
                var normalizedEmailToCheck = entity.Email.ToLowerInvariant();
                var existing = await _context.Person.AnyAsync(p => p.Email.ToLower() == normalizedEmailToCheck);
                if (existing)
                {
                    return Result<PersonEntity>.Failure("Email is already registered.");
                }

                // Maps domain entity to DB model (includes conditional password mapping).
                var personDbModel = MapToDbModel(entity);
                // Sets CreatedAt if not already set.
                if (personDbModel.Createdat == default)
                    personDbModel.Createdat = _dateTimeProvider.GetUtcNow();
                // Ensure other timestamps are null on creation.
                personDbModel.Updatedat = null;
                personDbModel.Lastloginat = null;
                personDbModel.Lastpasswordchangeat = null;

                // Adds to context and saves.
                await _context.Person.AddAsync(personDbModel);
                int success = await _context.SaveChangesAsync();

                // Returns failure if save failed.
                if (success == 0)
                    return Result<PersonEntity>.Failure("Failed to save person to the database.");

                // --- Workaround: Update domain entity ID post-save ---
                var idProperty = typeof(PersonEntity).GetProperty(nameof(PersonEntity.IdPerson));
                if (idProperty?.CanWrite ?? false)
                {
                    idProperty.SetValue(entity, personDbModel.Idperson, null);
                }
                else
                {
                    _logger.LogWarning("Could not set IdPerson on domain entity after creation.");
                }
                // --- End Workaround ---

                // Returns success with potentially updated domain entity.
                return Result<PersonEntity>.Success(entity);
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
            // Basic null check.
            if (entity == null)
                return Result<bool>.Failure("Person entity cannot be null.");

            try
            {
                // Finds existing entity (tracked).
                var existingPerson = await _context.Person.FindAsync(entity.IdPerson);
                // Returns failure if not found.
                if (existingPerson == null)
                    return Result<bool>.Failure("Person not found for update.");

                // --- Email Uniqueness Check on Change ---
                // If the email is being changed, check if the new email is already in use by another user.
                if (!string.Equals(existingPerson.Email, entity.Email, StringComparison.OrdinalIgnoreCase))
                {
                    var normalizedNewEmail = entity.Email.ToLowerInvariant();
                    var emailExists = await _context.Person.AnyAsync(p => p.Email.ToLower() == normalizedNewEmail && p.Idperson != entity.IdPerson);
                    if (emailExists)
                    {
                        return Result<bool>.Failure("Cannot update email, is already in use by another user.");
                    }
                }
                // --- End Email Check ---

                // Maps domain entity onto existing tracked entity (Password is NOT mapped here).
                MapToDbModel(entity, existingPerson);

                // Ensures UpdatedAt timestamp is set or updated.
                if (existingPerson.Updatedat == null || (entity.UpdatedAt.HasValue && existingPerson.Updatedat < entity.UpdatedAt.Value))
                    existingPerson.Updatedat = entity.UpdatedAt ?? _dateTimeProvider.GetUtcNow();
                else if (!entity.UpdatedAt.HasValue || (existingPerson.Updatedat.HasValue && entity.UpdatedAt.HasValue && existingPerson.Updatedat >= entity.UpdatedAt.Value)) 
                    existingPerson.Updatedat = _dateTimeProvider.GetUtcNow();


                // Marks for update and saves.
                _context.Person.Update(existingPerson);
                int rowsAffected = await _context.SaveChangesAsync();

                // Returns success based on rows affected.
                return rowsAffected > 0
                    ? Result<bool>.Success(true)
                    : Result<bool>.Failure("No changes were detected or saved for the person.");
            }
            catch (DbUpdateConcurrencyException ex) // Handle concurrency conflicts.
            {
                _logger.LogError(ex, "Concurrency conflict updating person with ID {IdPerson}. The record may have been modified or deleted.", entity.IdPerson); 
                return Result<bool>.Failure("Concurrency conflict updating person. The record may have been modified or deleted.");
            }
            catch (DbUpdateException dbEx) // Handle other DB update errors.
            {
                _logger.LogError(dbEx, "Database error updating person: {Message}", dbEx.InnerException?.Message ?? dbEx.Message); 
                return Result<bool>.Failure("Database error updating person.");
            }
            catch (Exception ex) // Handle general errors.
            {
                _logger.LogError(ex, "Error updating person: {Message}", ex.Message);
                return Result<bool>.Failure("Error updating person.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<bool>> Delete(int id)
        {
            try
            {
                // Finds entity by ID (tracked).
                var person = await _context.Person.FindAsync(id);
                // Returns failure if not found.
                if (person == null)
                    return Result<bool>.Failure("Person not found for deletion.");

                // Removes from context and saves.
                _context.Person.Remove(person);
                int rowsAffected = await _context.SaveChangesAsync();

                // Returns success based on rows affected.
                return rowsAffected > 0
                    ? Result<bool>.Success(true)
                    : Result<bool>.Failure("Failed to delete the person (no rows affected).");
            }
            catch (DbUpdateException dbEx) // Handle DB errors (e.g., FK constraints).
            {
                _logger.LogError(dbEx, "Database error deleting person (check for related records): {Message}", dbEx.InnerException?.Message ?? dbEx.Message); 
                return Result<bool>.Failure("Database error deleting person (check for related records).");
            }
            catch (Exception ex) // Handle general errors.
            {
                _logger.LogError(ex, "Error deleting person: {Message}", ex.Message);
                return Result<bool>.Failure("Error deleting person.");
            }
        }
    }
}