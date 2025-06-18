using System.ComponentModel.DataAnnotations;

namespace ArandanoIRT_Backend.Application.DTOs.Device
{
    /// <summary>
    /// Represents the response data sent to a device upon successful authentication or token refresh.
    /// </summary>
    public class DeviceAuthResponseDto
    {
        /// <summary>
        /// Gets or sets the new or validated access token for the device.
        /// </summary>
        [Required]
        public required string AccessToken { get; set; }

        /// <summary>
        /// Gets or sets the new refresh token for the device.
        /// </summary>
        [Required]
        public required string RefreshToken { get; set; }

        /// <summary>
        /// Gets or sets the date and time (UTC) when the access token expires.
        /// Useful for the device to know when to request a refresh.
        /// </summary>
        [Required]
        public required DateTime AccessTokenExpiration { get; set; }
    }
}