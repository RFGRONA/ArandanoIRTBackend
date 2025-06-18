using System.ComponentModel.DataAnnotations;

namespace ArandanoIRT_Backend.Application.DTOs.Objects
{
    /// <summary>
    /// Represents key weather data for a specific city obtained from an external service.
    /// Used to determine local time/daylight and city temperature/humidity.
    /// </summary>
    public class CityWeatherDto
    {
        /// <summary>
        /// Gets or sets the local date and time in the city.
        /// Includes offset from UTC.
        /// </summary>
        [Required]
        public required DateTime LocalTime { get; set; } 

        /// <summary>
        /// Gets or sets the current temperature in Celsius for the city.
        /// </summary>
        [Required]
        public required float TemperatureC { get; set; }

        /// <summary>
        /// Gets or sets the current humidity percentage for the city.
        /// </summary>
        [Required]
        [Range(0, 100)] // Humidity is a percentage
        public required float Humidity { get; set; }

        public required bool IsDay { get; set; }

        public string? TimeZoneId { get; set; }
    }
}