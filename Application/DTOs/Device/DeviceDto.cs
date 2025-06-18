namespace ArandanoIRT_Backend.Application.DTOs.Device
{
    /// <summary>
    /// Represents basic information about a device (camera) for listing purposes.
    /// Used for output to the UI (both admin and regular users).
    /// </summary>
    public class DeviceDto
    {
        /// <summary>
        /// Gets or sets the unique identifier for the device record.
        /// </summary>
        public required int IdDeviceData { get; set; } 

        /// <summary>
        /// Gets or sets the name of the device.
        /// </summary>
        public required string NameDevice { get; set; }

        /// <summary>
        /// Gets or sets a brief description of the device (can be truncated for list views).
        /// </summary>
        public string? DescriptionDevice { get; set; } 

        /// <summary>
        /// Gets or sets the current status ID of the device.
        /// </summary>
        public int? StatusId { get; set; } 

        /// <summary>
        /// Gets or sets the name of the current status of the device.
        /// </summary>
        public string? StatusName { get; set; }

        /// <summary>
        /// Gets or sets the ID of the crop to which this device is assigned.
        /// </summary>
        public int? CropId { get; set; } 

        /// <summary>
        /// Gets or sets the name of the specific crop this device is monitoring.
        /// </summary>
        public string? CropName { get; set; }

        /// <summary>
        /// Gets or sets the date and time (UTC) when the device was registered.
        /// </summary>
        public required DateTime RegisteredAt { get; set; }

    }
}