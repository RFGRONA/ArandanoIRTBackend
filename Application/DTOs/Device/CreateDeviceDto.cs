using System.ComponentModel.DataAnnotations;

namespace ArandanoIRT_Backend.Application.DTOs.Device
{
    /// <summary>
    /// Represents the data required to create a new device (camera) record.
    /// Used for input from the administrator UI.
    /// </summary>
    public class CreateDeviceDto
    {
        /// <summary>
        /// Gets or sets the name of the device.
        /// </summary>
        [Required]
        [StringLength(50)]
        public required string NameDevice { get; set; }

        /// <summary>
        /// Gets or sets a description for the device.
        /// </summary>
        [StringLength(1000)] 
        public string? DescriptionDevice { get; set; } 

        /// <summary>
        /// Gets or sets the configured time interval in seconds for data collection by the device.
        /// </summary>
        [Required]
        [Range(1, short.MaxValue, ErrorMessage = "Data collection time must be a positive value.")] 
        public required short DataCollectionTime { get; set; }

        /// <summary>
        /// Gets or sets the initial status ID for the device.
        /// Optional, as the backend might set a default (e.g., Inactive or Maintenance) upon creation.
        /// </summary>
        public int? StatusId { get; set; }

        /// <summary>
        /// Gets or sets the ID of the crop to which this device is initially assigned. Optional.
        /// </summary>
        public int? CropId { get; set; }

        /// <summary>
        /// Gets or sets the ID of the specific plant this device is initially monitoring. Optional.
        /// Requires CropId to be set if PlantId is set (business rule).
        /// </summary>
        public int? PlantId { get; set; }
    }
}