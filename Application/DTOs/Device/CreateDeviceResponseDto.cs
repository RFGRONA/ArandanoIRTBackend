namespace ArandanoIRT_Backend.Application.DTOs.Device
{
    /// <summary>
    /// Represents the response data returned after successfully creating a new device record,
    /// including its details and the initial activation information.
    /// </summary>
    public class CreateDeviceResponseDto
    {
        /// <summary>
        /// Gets or sets the detailed information about the newly created device.
        /// </summary>
        public required DeviceDetailDto DeviceDetails { get; set; }

        /// <summary>
        /// Gets or sets the unique activation code generated for the new device.
        /// This code is needed for the physical device to activate itself in the system.
        /// </summary>
        public required string ActivationCode { get; set; }

        /// <summary>
        /// Gets or sets the ID of the activation record associated with this code.
        /// </summary>
        public required int ActivationId { get; set; }
    }
}