namespace ArandanoIRT_Backend.Application.DTOs.Device
{
    /// <summary>
    /// Represents detailed information about a device (camera).
    /// Used for output to the UI (both admin and regular users).
    /// </summary>
    public class DeviceDetailDto
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
        /// Gets or sets a detailed description for the device.
        /// </summary>
        public string? DescriptionDevice { get; set; }

        /// <summary>
        /// Gets or sets the configured time interval in seconds for data collection.
        /// </summary>
        public required short DataCollectionTime { get; set; }

        /// <summary>
        /// Gets or sets the current status ID of the device.
        /// </summary>
        public int? StatusId { get; set; }

        /// <summary>
        /// Gets or sets the name of the current status of the device.
        /// </summary>
        public string? StatusName { get; set; }


        /// <summary>
        /// Gets or sets the date and time (UTC) when the device was registered in the system.
        /// </summary>
        public required DateTime RegisteredAt { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier of the user who registered the device. Optional.
        /// </summary>
        public int? RegisteredBy { get; set; }

        /// <summary>
        /// Gets or sets the name of the user who registered the device. Optional.
        /// </summary>
        public string? RegisteredByName { get; set; }


        /// <summary>
        /// Gets or sets the date and time (UTC) when the device record was last updated. Optional.
        /// </summary>
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier of the user who last updated the device record. Optional.
        /// </summary>
        public int? UpdatedBy { get; set; }

        /// <summary>
        /// Gets or sets the name of the user who last updated the device record. Optional.
        /// </summary>
        public string? UpdatedByName { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier of the crop to which this device is assigned. Optional.
        /// </summary>
        public int? CropId { get; set; }

        /// <summary>
        ///  Gets or sets the name of the crop to which this device is assigned. Optional.
        /// </summary>
        public string? CropName { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier of the specific plant this device is monitoring. Optional.
        /// </summary>
        public int? PlantId { get; set; }

        /// <summary>
        /// Gets or sets the name of the specific plant this device is monitoring. Optional.
        /// </summary>
        public string? PlantName { get; set; }

    }
}