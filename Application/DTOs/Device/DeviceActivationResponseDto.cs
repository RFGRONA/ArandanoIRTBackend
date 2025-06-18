using System.ComponentModel.DataAnnotations;

namespace ArandanoIRT_Backend.Application.DTOs.Device
{
    /// <summary>
    /// Represents the response data sent to a device upon successful activation,
    /// providing authentication tokens.
    /// </summary>
    public class DeviceActivationResponseDto
    {
        /// <summary>
        /// Gets or sets the access token for the device.
        /// </summary>
        [Required]
        public required string AccessToken { get; set; }

        /// <summary>
        /// Gets or sets the refresh token for the device, used to obtain new access tokens.
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