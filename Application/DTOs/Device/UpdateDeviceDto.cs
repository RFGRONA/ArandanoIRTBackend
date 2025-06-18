using System.ComponentModel.DataAnnotations;

namespace ArandanoIRT_Backend.Application.DTOs.Device
{
    /// <summary>
    /// Represents the data used to update an existing device (camera) record.
    /// Used for input from the administrator UI.
    /// </summary>
    public class UpdateDeviceDto
    {

        /// <summary>
        /// Gets or sets the updated name of the device.
        /// </summary>
        [Required] 
        [StringLength(50)]
        public required string NameDevice { get; set; }

        /// <summary>
        /// Gets or sets the updated description for the device.
        /// </summary>
        [StringLength(1000)]
        public string? DescriptionDevice { get; set; } 

        /// <summary>
        /// Gets or sets the updated time interval in seconds for data collection.
        /// </summary>
        [Required]
        [Range(1, short.MaxValue, ErrorMessage = "Data collection time must be a positive value.")]
        public required short DataCollectionTime { get; set; }

        /// <summary>
        /// Gets or sets the updated status ID for the device.
        /// </summary>
        public int? StatusId { get; set; } 

        /// <summary>
        /// Gets or sets the updated ID of the crop to which this device is assigned. Optional.
        /// </summary>
        public int? CropId { get; set; }

        /// <summary>
        /// Gets or sets the updated ID of the specific plant this device is monitoring. Optional.
        /// Requires CropId to be set if PlantId is set.
        /// </summary>
        public int? PlantId { get; set; }
    }
}