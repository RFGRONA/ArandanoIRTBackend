using System.ComponentModel.DataAnnotations;

namespace ArandanoIRT_Backend.Application.DTOs.Device
{
    /// <summary>
    /// Represents the response data from the device token service after generating or refreshing tokens.
    /// Includes both access and refresh token information.
    /// </summary>
    public class DeviceTokenResponseDto
    {
        /// <summary>
        /// Gets or sets the generated or current opaque access token.
        /// </summary>
        [Required]
        public required string AccessToken { get; set; }

        /// <summary>
        /// Gets or sets the generated or current opaque refresh token.
        /// </summary>
        [Required]
        public required string RefreshToken { get; set; }

        /// <summary>
        /// Gets or sets the date and time (UTC) when the access token expires.
        /// </summary>
        [Required]
        public required DateTime AccessTokenExpiration { get; set; }

        /// <summary>
        /// Gets or sets the date and time (UTC) when the refresh token expires.
        /// </summary>
        [Required]
        public required DateTime RefreshTokenExpiration { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier of the DeviceToken record in the database.
        /// Useful for tracking the refresh token record.
        /// </summary>
        [Required]
        public required int DeviceTokenId { get; set; }

        /// <summary>
        /// Gets or sets the ID of the device associated with these tokens. Nullable if not directly linked at DTO level.
        /// </summary>
        public int? DeviceId { get; set; } 
    }
}