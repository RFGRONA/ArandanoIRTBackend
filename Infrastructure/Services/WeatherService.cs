using ArandanoIRT_Backend.Application.DTOs.Objects;
using ArandanoIRT_Backend.Application.Interfaces.Utilities; 
using ArandanoIRT_Backend.Domain.ValueObjects; 
using Serilog;
using System.Text.Json;

namespace ArandanoIRT_Backend.Infrastructure.Services
{
    /// <summary>
    /// Implements the <see cref="IWeatherService"/> interface, retrieving weather data from WeatherAPI.com.
    /// Includes caching of results.
    /// </summary>
    public class WeatherService : IWeatherService
    {
        private readonly HttpClient _httpClient;
        private readonly ICacheService _cacheService;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly ILogger<WeatherService> _logger;
        private readonly string _apiKey;
        private readonly string _baseUrl;
        private readonly TimeSpan _cacheDuration;

        // Constants for configuration keys
        private const string API_KEY_CONFIG_KEY = "WeatherApi:Key";
        private const string BASE_URL_CONFIG_KEY = "WeatherApi:BaseUrl";
        private const string CACHE_DURATION_CONFIG_KEY = "WeatherApi:CacheDurationMinutes"; 

        // Cache key format for city weather data
        private const string CACHE_KEY_FORMAT = "Weather_{0}_{1}_{2}"; // {0}:city, {1}:state/province, {2}:country

        /// <summary>
        /// Initializes a new instance of the <see cref="WeatherService"/> class.
        /// </summary>
        /// <param name="httpClient">The HttpClient instance.</param>
        /// <param name="cacheService">The caching service.</param>
        /// <param name="dateTimeProvider">The date and time provider.</param>
        /// <param name="configuration">The application configuration.</param>
        /// <param name="logger">The logger instance.</param>
        /// <exception cref="ArgumentNullException">Thrown if dependencies are null.</exception>
        /// <exception cref="InvalidOperationException">Thrown if required configuration is missing or invalid.</exception>
        public WeatherService(
            HttpClient httpClient,
            ICacheService cacheService,
            IDateTimeProvider dateTimeProvider,
            IConfiguration configuration,
            ILogger<WeatherService> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Read configuration
            _apiKey = configuration[API_KEY_CONFIG_KEY] ?? throw new InvalidOperationException($"Configuration missing required key.");
            _baseUrl = configuration[BASE_URL_CONFIG_KEY] ?? throw new InvalidOperationException($"Configuration missing required key.");

            // Read cache duration
            if (!int.TryParse(configuration[CACHE_DURATION_CONFIG_KEY], out int cacheDurationMinutes) || cacheDurationMinutes <= 0)
            {
                _logger.LogWarning("Configuration key {ConfigKey} is missing or invalid. Using default cache duration of 30 minutes.", CACHE_DURATION_CONFIG_KEY);
                _cacheDuration = TimeSpan.FromMinutes(30); // Default cache duration
            }
            else
            {
                _cacheDuration = TimeSpan.FromMinutes(cacheDurationMinutes);
            }


            // Ensure base URL is set on HttpClient if not already configured
            if (_httpClient.BaseAddress == null)
            {
                try
                {
                    _httpClient.BaseAddress = new Uri(_baseUrl);
                }
                catch (UriFormatException ex)
                {
                    _logger.LogError(ex, "Invalid base URL configured for WeatherAPI: {BaseUrl}", _baseUrl);
                    throw new InvalidOperationException("Invalid base URL configured for WeatherAPI.");
                }
            }

            _logger.LogInformation("WeatherService initialized. Base URL: {BaseUrl}, Cache Duration: {CacheDuration}", _baseUrl, _cacheDuration);
        }

        /// <inheritdoc/>
        public async Task<Result<CityWeatherDto>> GetCityWeatherAsync(string city, string? stateProvince, string country)
        {
            if (string.IsNullOrWhiteSpace(city)) return Result<CityWeatherDto>.Failure("City name is required.");
            if (string.IsNullOrWhiteSpace(country)) return Result<CityWeatherDto>.Failure("Country name is required.");

            // Generate cache key
            var cacheKey = GenerateCacheKey(city, stateProvince, country);

            // 1. Try getting from cache
            var cachedWeather = _cacheService.Get<CityWeatherDto>(cacheKey);
            if (cachedWeather != null)
            {
                _logger.LogDebug("Cache hit for weather data: {CacheKey}", cacheKey);
                return Result<CityWeatherDto>.Success(cachedWeather);
            }

            // 2. Cache miss, fetch from API
            _logger.LogDebug("Cache miss for weather data: {CacheKey}. Fetching from API.", cacheKey);
            try
            {
                var weatherDto = await FetchWeatherFromApiAsync(city, stateProvince, country);

                // 3. Store in cache if successful
                if (weatherDto.IsSuccess)
                {
                    _cacheService.Set(cacheKey, weatherDto.Value, _cacheDuration);
                    _logger.LogInformation("Cached weather data for {CacheKey}", cacheKey);
                }

                return weatherDto; // Return the result from API fetch
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while fetching weather data for {City}, {Country}.", city, country);
                return Result<CityWeatherDto>.Failure("An error occurred while retrieving weather data.");
            }
        }

        /// <summary>
        /// Generates a unique cache key for a given city location.
        /// </summary>
        /// <param name="city">City name.</param>
        /// <param name="stateProvince">State or province name.</param>
        /// <param name="country">Country name.</param>
        /// <returns>A unique string cache key.</returns>
        private static string GenerateCacheKey(string city, string? stateProvince, string country)
        {
            // Use consistent formatting for the key
            return string.Format(CACHE_KEY_FORMAT,
                                city.ToLowerInvariant(),
                                stateProvince?.ToLowerInvariant() ?? "n/a", // Use "n/a" for null state/province
                                country.ToLowerInvariant());
        }

        /// <summary>
        /// Fetches weather data from the WeatherAPI.com API.
        /// </summary>
        /// <param name="city">City name.</param>
        /// <param name="stateProvince">State or province name.</param>
        /// <param name="country">Country name.</param>
        /// <returns>A Result containing CityWeatherDto on success, or failure.</returns>
        private async Task<Result<CityWeatherDto>> FetchWeatherFromApiAsync(string city, string? stateProvince, string country)
        {
            // Build the query string
            // WeatherAPI.com uses 'q' parameter for query. Combine location components.
            // Format: city,state,country or city,country
            string locationQuery = $"{city},{stateProvince},{country}";
            if (string.IsNullOrWhiteSpace(stateProvince))
            {
                locationQuery = $"{city},{country}";
            }

            // Build the API endpoint URL for current weather
            var apiUrl = $"v1/current.json?key={_apiKey}&q={Uri.EscapeDataString(locationQuery)}";

            _logger.LogDebug("Fetching weather data from API using query: {LocationQuery}", locationQuery);

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.GetAsync(apiUrl);
            }
            catch (HttpRequestException httpEx)
            {
                _logger.LogError(httpEx, "HTTP request failed when fetching weather data for {LocationQuery}.", locationQuery);
                return Result<CityWeatherDto>.Failure("Failed to connect to weather service.");
            }

            // Handle API response based on status code
            if (!response.IsSuccessStatusCode)
            {
                var errorResult = await HandleApiErrorResponseAsync(response, locationQuery);
                return Result<CityWeatherDto>.Failure(errorResult.ErrorMessage ?? "Weather service returned an error.");
            }

            // Read and deserialize the successful response
            try
            {
                var jsonResponse = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<WeatherApiResponse>(jsonResponse); // Use helper types for API response

                // Map the API response structure to our domain DTO
                var weatherDto = MapApiResponseToCityWeatherDto(apiResponse);

                if (weatherDto == null)
                {
                    _logger.LogError("Failed to map WeatherAPI response to CityWeatherDto for {LocationQuery}. Response: {ResponseJson}", locationQuery, jsonResponse);
                    return Result<CityWeatherDto>.Failure("Failed to process weather data response.");
                }

                _logger.LogDebug("Successfully fetched and mapped weather data for {LocationQuery}.", locationQuery);
                return Result<CityWeatherDto>.Success(weatherDto);

            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "Failed to deserialize weather API response for {LocationQuery}.", locationQuery);
                return Result<CityWeatherDto>.Failure("Failed to process weather data response.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while processing weather API response for {LocationQuery}.", locationQuery);
                return Result<CityWeatherDto>.Failure("An error occurred while retrieving weather data.");
            }
        }

        /// <summary>
        /// Handles non-success HTTP responses from the WeatherAPI.com API.
        /// Attempts to extract and log specific API errors.
        /// </summary>
        /// <param name="response">The HttpResponseMessage with a non-success status code.</param>
        /// <param name="locationQuery">The original location query string.</param>
        /// <returns>A failed Result with an appropriate error message.</returns>
        private async Task<Result> HandleApiErrorResponseAsync(HttpResponseMessage response, string locationQuery)
        {
            string errorBody = string.Empty;
            try
            {
                errorBody = await response.Content.ReadAsStringAsync();
                // Attempt to deserialize WeatherAPI error structure if available
                var apiErrorResponse = JsonSerializer.Deserialize<WeatherApiErrorResponse>(errorBody);
                if (apiErrorResponse?.Error != null)
                {
                    _logger.LogWarning("WeatherAPI returned error status {StatusCode} for {LocationQuery}. Code: {ErrorCode}, Message: {ErrorMessage}",
                                      response.StatusCode, locationQuery, apiErrorResponse.Error.Code, apiErrorResponse.Error.Message);
                    // Return specific API error message if helpful, otherwise a generic one.
                    // Based on provided error codes, some messages like "No location found" are user-facing.
                    if (apiErrorResponse.Error.Code == 1006) return Result.Failure($"Location not found: {locationQuery}");
                    // For other errors, return a generic message
                    return Result.Failure($"Weather service error: {apiErrorResponse.Error.Message}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read or deserialize WeatherAPI error response body for {LocationQuery}. Status: {StatusCode}. Body: {ErrorBody}", locationQuery, response.StatusCode, errorBody);
            }

            // Fallback for unexpected error responses or deserialization failures
            _logger.LogError("WeatherAPI returned non-success status {StatusCode} for {LocationQuery}. Response body: {ErrorBody}", response.StatusCode, locationQuery, errorBody);
            return Result.Failure($"Weather service returned an unexpected error (Status: {response.StatusCode}).");
        }


        /// <summary>
        /// Maps the deserialized WeatherAPI response object structure to a CityWeatherDto.
        /// </summary>
        /// <param name="apiResponse">The deserialized response object from WeatherAPI.com.</param>
        /// <returns>The mapped CityWeatherDto, or null if mapping fails.</returns>
        private static CityWeatherDto? MapApiResponseToCityWeatherDto(WeatherApiResponse? apiResponse)
        {
            if (apiResponse?.Location == null || apiResponse.Current == null)
            {
                return null; // Cannot map if essential parts are missing
            }

            try
            {
                // WeatherAPI returns "localtime" as a string like "2023-01-13 14:30". Parse this string.
                if (!DateTime.TryParse(apiResponse.Location.Localtime, out DateTime localTime))
                {
                    // Log warning if localtime string format is unexpected
                    Log.Warning("Failed to parse WeatherAPI localtime string: {LocaltimeString}", apiResponse.Location.Localtime);
                    return null;
                }


                return new CityWeatherDto
                {
                    LocalTime = localTime, // Parsed local time
                    TemperatureC = apiResponse.Current.TempC, // Use TempC
                    Humidity = apiResponse.Current.Humidity,
                    IsDay = apiResponse.Current.IsDay == 1, // Map 1/0 to bool
                    TimeZoneId = apiResponse.Location.TzId 
                };
            }
            catch (Exception ex)
            {
                // Log mapping errors
                Log.Error(ex, "Error mapping WeatherAPI response to CityWeatherDto.");
                return null; // Indicate mapping failure
            }
        }

        // --- Helper classes to match WeatherAPI JSON response structure ---

        private class WeatherApiResponse
        {
            public LocationData? Location { get; set; }
            public CurrentData? Current { get; set; }
        }

        private class LocationData
        {
            public string? Name { get; set; }
            public string? Region { get; set; }
            public string? Country { get; set; }
            public float Lat { get; set; }
            public float Lon { get; set; }
            public string? TzId { get; set; } // Time Zone ID
            public int LocaltimeEpoch { get; set; } // Local time in unix epoch
            public string? Localtime { get; set; } // Local time in "yyyy-MM-dd HH:mm" format
        }

        private class CurrentData
        {
            // Add all relevant fields from the 'current' object in the API response
            public float TempC { get; set; }
            public float TempF { get; set; }
            public int IsDay { get; set; } // 1 = Yes, 0 = No
            public int Humidity { get; set; } // Humidity as percentage
                                              // Add other fields like wind, pressure, condition, etc., if needed.
            public float FeelslikeC { get; set; } // Example: add feels like temp
        }

        private class WeatherApiErrorResponse
        {
            public ErrorDetails? Error { get; set; }
        }

        private class ErrorDetails
        {
            public int Code { get; set; }
            public string? Message { get; set; }
        }

    }
}