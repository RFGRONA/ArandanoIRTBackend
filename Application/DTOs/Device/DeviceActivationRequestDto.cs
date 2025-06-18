using System.ComponentModel.DataAnnotations;

namespace ArandanoIRT_Backend.Application.DTOs.Device
{
    /// <summary>
    /// Represents the request data sent by a device to activate itself using a code.
    /// </summary>
    public class DeviceActivationRequestDto
    {
        /// <summary>
        /// Gets or sets the unique ID of the device provided during registration.
        /// </summary>
        [Required] 
        public required int DeviceId { get; set; } 

        /// <summary>
        /// Gets or sets the activation code provided during registration.
        /// </summary>
        [Required]
        [StringLength(255)] 
        public required string ActivationCode { get; set; }
    }
}