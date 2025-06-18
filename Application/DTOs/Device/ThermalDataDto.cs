using System.ComponentModel.DataAnnotations; 

namespace ArandanoIRT_Backend.Application.DTOs.Device
{
    /// <summary>
    /// Represents the JSON part of the thermal and image data payload sent by a device,
    /// containing thermal readings and calculated statistics.
    /// </summary>
    public class ThermalDataDto
    {
        /// <summary>
        /// Gets or sets the maximum temperature reading from the thermal sensor (MLX90640).
        /// Corresponds to the "max_temp" field in the device's JSON payload.
        /// </summary>
        [Required] 
        public required float MaxTemp { get; set; }

        /// <summary>
        /// Gets or sets the minimum temperature reading from the thermal sensor (MLX90640).
        /// Corresponds to the "min_temp" field in the device's JSON payload.
        /// </summary>
        [Required] 
        public required float MinTemp { get; set; }

        /// <summary>
        /// Gets or sets the average temperature calculated from the thermal sensor data.
        /// Corresponds to the "avg_temp" field in the device's JSON payload.
        /// </summary>
        [Required] 
        public required float AvgTemp { get; set; }

        /// <summary>
        /// Gets or sets the array of raw thermal pixel temperatures.
        /// Corresponds to the "temperatures" array in the device's JSON payload.
        /// Can contain null values for pixels with invalid readings (NaN in firmware).
        /// </summary>
        [Required] 
        public required float?[] Temperatures { get; set; } 
    }
}