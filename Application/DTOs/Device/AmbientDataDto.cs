using System.ComponentModel.DataAnnotations;

namespace ArandanoIRT_Backend.Application.DTOs.Device
{
    /// <summary>
    /// Represents the environmental data payload sent by a device.
    /// </summary>
    public class AmbientDataDto
    {
        /// <summary>
        /// Gets or sets the light intensity reading from the device.
        /// Corresponds to the "light" field in the device's JSON payload.
        /// </summary>
        [Required] 
        public required float Light { get; set; }

        /// <summary>
        /// Gets or sets the temperature reading from the device (e.g., from DHT22).
        /// Corresponds to the "temperature" field in the device's JSON payload.
        /// </summary>
        [Required] 
        public required float Temperature { get; set; }

        /// <summary>
        /// Gets or sets the humidity reading from the device (e.g., from DHT22).
        /// Corresponds to the "humidity" field in the device's JSON payload.
        /// </summary>
        [Required]
        public required float Humidity { get; set; }

        // Note: Additional fields like cityTemperature, cityHumidity might be added later if the device
        // sends them or if they are retrieved by the backend from an external source and included in a combined DTO.
    }
}