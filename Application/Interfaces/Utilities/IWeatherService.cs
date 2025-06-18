using ArandanoIRT_Backend.Application.DTOs.Objects;
using ArandanoIRT_Backend.Domain.ValueObjects; 

namespace ArandanoIRT_Backend.Application.Interfaces.Utilities
{
    /// <summary>
    /// Defines the interface for the service responsible for retrieving external weather data for a city.
    /// </summary>
    public interface IWeatherService
    {
        /// <summary>
        /// Retrieves current weather data (local time, temperature, humidity, is_day status)
        /// for a specified city location.
        /// Implementations should handle caching of results.
        /// </summary>
        /// <param name="city">The name of the city.</param>
        /// <param name="stateProvince">The name of the state or province (optional, can help disambiguate cities).</param>
        /// <param name="country">The name of the country (important for accurate location lookup).</param>
        /// <returns>
        /// A <see cref="Result{T}"/> indicating success and the <see cref="CityWeatherDto"/> containing
        /// the weather data, or failure if the data could not be retrieved (e.g., city not found, API error).
        /// </returns>
        Task<Result<CityWeatherDto>> GetCityWeatherAsync(string city, string? stateProvince, string country);
    }
}