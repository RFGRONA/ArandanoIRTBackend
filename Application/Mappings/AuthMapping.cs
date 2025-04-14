using ArandanoIRT_Backend.Application.DTOs.Auth;
using ArandanoIRT_Backend.Domain.Entities;

namespace ArandanoIRT_Backend.Application.Mappings
{
    /// <summary>
    /// Provides static mapping methods for authentication related DTOs and Entities.
    /// </summary>
    public static class AuthMapping
    {
        /// <summary>
        /// Maps the AdminInfo part of a RegisterAdminRequestDto to a new PersonEntity.
        /// The returned entity has the plain password and null CropId; these require processing by the calling service.
        /// </summary>
        /// <param name="dto">The DTO containing administrator information.</param>
        /// <param name="createdAt">The creation timestamp.</param>
        /// <returns>A new PersonEntity instance configured as an administrator, ready for further processing (hashing, CropId assignment).</returns>
        public static PersonEntity ToAdminPersonEntity(this RegisterAdminRequestDto dto, DateTime createdAt)
        {
            if (dto?.AdminInfo == null)
                throw new ArgumentNullException(nameof(dto), "Admin registration data cannot be null.");

            return new PersonEntity(
                idPerson: 0, 
                firstName: dto.AdminInfo.FirstName,
                lastName: dto.AdminInfo.LastName,
                email: dto.AdminInfo.Email.ToLowerInvariant(), 
                password: dto.AdminInfo.Password, 
                createdAt: createdAt,
                isAdmin: true, 
                allNotifications: true, 
                cropId: null 
            );
        }

        /// <summary>
        /// Maps the CropInfo part of a RegisterAdminRequestDto to a new CropEntity.
        /// The returned entity has a null AdminUserId; requires assignment by the calling service.
        /// </summary>
        /// <param name="dto">The DTO containing crop information.</param>
        /// <param name="createdAt">The creation timestamp.</param>
        /// <returns>A new CropEntity instance, ready for further processing (AdminUserId assignment).</returns>
        public static CropEntity ToCropEntity(this RegisterAdminRequestDto dto, DateTime createdAt)
        {
            if (dto?.CropInfo == null)
                throw new ArgumentNullException(nameof(dto), "Crop registration data cannot be null.");

            return new CropEntity(
                idCrop: 0, 
                nameCrop: dto.CropInfo.NameCrop,
                addressCrop: dto.CropInfo.AddresCrop, 
                cityName: dto.CropInfo.UbicationCrop, 
                createdAt: createdAt,
                adminUserId: null 
            );
        }

        /// <summary>
        /// Maps a RegisterUserRequestDto to a new PersonEntity.
        /// The returned entity has the plain password; password hashing is handled by the calling service.
        /// </summary>
        /// <param name="dto">The DTO containing user information.</param>
        /// <param name="createdAt">The creation timestamp.</param>
        /// <param name="cropId">The Crop ID associated with this user (e.g., from a validated invitation).</param>
        /// <returns>A new PersonEntity instance configured as a regular user, ready for password hashing.</returns>
        public static PersonEntity ToUserPersonEntity(this RegisterUserRequestDto dto, DateTime createdAt, int cropId)
        {
            if (dto?.UserInfo == null)
                throw new ArgumentNullException(nameof(dto), "User registration data cannot be null.");
            if (cropId <= 0)
                throw new ArgumentException("Valid CropId must be provided.", nameof(cropId));

            return new PersonEntity(
                idPerson: 0, 
                firstName: dto.UserInfo.FirstName,
                lastName: dto.UserInfo.LastName,
                email: dto.UserInfo.Email.ToLowerInvariant(), 
                password: dto.UserInfo.Password, 
                createdAt: createdAt,
                isAdmin: false, 
                allNotifications: true, 
                cropId: cropId 
            );
        }

        /// <summary>
        /// Maps a PersonEntity to a LoginResponseDto.
        /// Note: The 'Token' property in the returned DTO is initialized as empty and must be set by the calling service.
        /// </summary>
        /// <param name="entity">The PersonEntity retrieved after successful login.</param>
        /// <returns>A LoginResponseDto containing user details, excluding the authentication token.</returns>
        public static LoginResponseDto ToLoginResponseDto(this PersonEntity entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity), "Person entity cannot be null for login response.");

            return new LoginResponseDto
            {
                Token = string.Empty, 
                IdUser = entity.IdPerson,
                Username = $"{entity.FirstName} {entity.LastName}".Trim(), 
                Email = entity.Email,
                Role = entity.IsAdmin ? "Admin" : "User", 
                IdCrop = entity.CropId ?? 0
            };
        }
    }
}