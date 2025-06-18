using System.ComponentModel.DataAnnotations;

namespace ArandanoIRT_Backend.Application.DTOs.Device
{
    /// <summary>
    /// Represents a single log entry sent by a device.
    /// </summary>
    public class DeviceLogEntryDto
    {
        /// <summary>
        /// Gets or sets the type or category of the log entry (e.g., "INFO", "WARNING", "ERROR").
        /// </summary>
        [Required]
        [StringLength(50)] 
        public required string LogType { get; set; }

        /// <summary>
        /// Gets or sets the detailed message content of the log entry.
        /// </summary>
        [Required]
        [StringLength(1000)]
        public required string LogMessage { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the log event occurred on the device.
        /// Required for accurate logging, using UTC.
        /// </summary>
        [Required] 
        public required DateTime LogTimestamp { get; set; }
    }
}