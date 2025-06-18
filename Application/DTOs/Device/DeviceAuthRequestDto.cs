using System.ComponentModel.DataAnnotations;

namespace ArandanoIRT_Backend.Application.DTOs.Device
{
    /// <summary>
    /// Represents the request data sent by a device to authenticate or refresh its token.
    /// </summary>
    public class DeviceAuthRequestDto
    {
        /// <summary>
        /// Gets or sets the current authentication token (Access Token or Refresh Token, depending on the endpoint).
        /// </summary>
        [Required]
        public required string Token { get; set; }
    }
}