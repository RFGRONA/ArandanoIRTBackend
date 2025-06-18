// File: ArandanoIRT_Backend.Application.Services/DeviceCommunicationService.cs
using ArandanoIRT_Backend.Application.DTOs.Device;
using ArandanoIRT_Backend.Application.DTOs.Objects; // For CityWeatherDto
using ArandanoIRT_Backend.Application.Interfaces.IServices;
using ArandanoIRT_Backend.Application.Interfaces.Utilities; // For IDeviceTokenService, IWeatherService, IDateTimeProvider, ICacheService (used by WeatherService)
using ArandanoIRT_Backend.Application.Mappings; // For mappings
using ArandanoIRT_Backend.Domain.Entities;
using ArandanoIRT_Backend.Domain.IRepositories;
using ArandanoIRT_Backend.Domain.ValueObjects; // For Result
using ArandanoIRT_Backend.Infrastructure.Repositories;
using Microsoft.AspNetCore.Http; // For IFormFile
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.IO; // For MemoryStream
using System.Threading.Tasks;

namespace ArandanoIRT_Backend.Application.Services
{
    /// <summary>
    /// Implements the <see cref="IDeviceCommunicationService"/> interface, providing application logic
    /// for handling direct communication with devices (activation, authentication, data reception).
    /// </summary>
    public class DeviceCommunicationService : IDeviceCommunicationService
    {
        private readonly IDeviceDataRepository _deviceDataRepository;
        private readonly IDeviceActivationRepository _deviceActivationRepository;
        private readonly IDeviceTokenService _deviceTokenService; 
        private readonly IDeviceLogRepository _deviceLogRepository; 
        private readonly ISensorDataRepository _sensorDataRepository; 
        private readonly IThermalDataRepository _thermalDataRepository; 
        private readonly IWeatherService _weatherService; 
        private readonly ICropRepository _cropRepository; 
        private readonly IStatusRepository _statusRepository;
        private readonly IDateTimeProvider _dateTimeProvider; 
        private readonly ILogger<DeviceCommunicationService> _logger;

        // Assuming StatusRepository exists to get Status names by ID
        // private readonly IStatusRepository _statusRepository; // Already injected in DeviceManagementService, might need here if checking device status


        /// <summary>
        /// Initializes a new instance of the <see cref="DeviceCommunicationService"/> class.
        /// </summary>
        /// <param name="deviceDataRepository">The repository for device data.</param>
        /// <param name="deviceActivationRepository">The repository for device activation records.</param>
        /// <param name="deviceTokenService">The service for device token management.</param>
        /// <param name="deviceLogRepository">The repository for device logs.</param>
        /// <param name="sensorDataRepository">The repository for sensor data.</param>
        /// <param name="thermalDataRepository">The repository for thermal data.</param>
        /// <param name="weatherService">The service for external weather data.</param>
        /// <param name="cropRepository">The repository for crop data.</param>
        /// <param name="statusRepository">The repository for status data.</param>
        /// <param name="dateTimeProvider">The provider for current date and time.</param>
        /// <param name="logger">The logger instance.</param>
        /// <exception cref="ArgumentNullException">Thrown if any dependency is null.</exception>
        public DeviceCommunicationService(
            IDeviceDataRepository deviceDataRepository,
            IDeviceActivationRepository deviceActivationRepository,
            IDeviceTokenService deviceTokenService,
            IDeviceLogRepository deviceLogRepository,
            ISensorDataRepository sensorDataRepository,
            IThermalDataRepository thermalDataRepository,
            IWeatherService weatherService,
            ICropRepository cropRepository,
            IStatusRepository statusRepository,
            IDateTimeProvider dateTimeProvider,
            ILogger<DeviceCommunicationService> logger)
        {
            _deviceDataRepository = deviceDataRepository ?? throw new ArgumentNullException(nameof(deviceDataRepository));
            _deviceActivationRepository = deviceActivationRepository ?? throw new ArgumentNullException(nameof(deviceActivationRepository));
            _deviceTokenService = deviceTokenService ?? throw new ArgumentNullException(nameof(deviceTokenService));
            _deviceLogRepository = deviceLogRepository ?? throw new ArgumentNullException(nameof(deviceLogRepository));
            _sensorDataRepository = sensorDataRepository ?? throw new ArgumentNullException(nameof(sensorDataRepository));
            _thermalDataRepository = thermalDataRepository ?? throw new ArgumentNullException(nameof(thermalDataRepository));
            _weatherService = weatherService ?? throw new ArgumentNullException(nameof(weatherService));
            _cropRepository = cropRepository ?? throw new ArgumentNullException(nameof(cropRepository));
            _statusRepository = statusRepository ?? throw new ArgumentNullException(nameof(statusRepository));
            _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }


        /// <inheritdoc/>
        public async Task<Result<DeviceActivationResponseDto>> ActivateDeviceAsync(DeviceActivationRequestDto requestDto, string ipAddress, string userAgent, string? deviceInfo)
        {
            _logger.LogInformation("Attempting device activation for Device ID: {DeviceId} with code.", requestDto?.DeviceId);

            if (requestDto == null)
            {
                _logger.LogWarning("ActivateDeviceAsync called with null DTO.");
                return Result<DeviceActivationResponseDto>.Failure("Activation data cannot be null.");
            }
            // DTO validation attributes handle basic checks on requestDto.

            try
            {
                // 1. Validate the activation code and check if it's pending and unexpired
                var activationResult = await _deviceActivationRepository.GetByActivationCodeAsync(requestDto.ActivationCode);

                if (activationResult.IsFailure)
                {
                    _logger.LogWarning("Device activation failed for code. Reason: {Error}", activationResult.ErrorMessage);
                    return Result<DeviceActivationResponseDto>.Failure("Invalid or expired activation code."); // Generic user message
                }
                var activationEntity = activationResult.Value;

                // 2. Verify Device ID matches the activation record (security check)
                if (activationEntity.DeviceId != requestDto.DeviceId)
                {
                    _logger.LogWarning("Device activation failed: Provided Device ID {ProvidedId} does not match Activation Record Device ID {RecordId}.", requestDto.DeviceId, activationEntity.DeviceId);
                    _logger.LogWarning("Attempted activation with code matching record ID {ActivationId} for incorrect Device ID {ProvidedId}.", activationEntity.IdDeviceActivation, requestDto.DeviceId);
                    return Result<DeviceActivationResponseDto>.Failure("Invalid activation data."); // Generic user message
                }

                // The activation record is valid and matches the provided device ID.

                // 3. Mark the activation record as 'Activated'
                // Need the Status ID for 'activo'. Assuming DeviceActivationRepository exposes status IDs or has a method.
                // Re-using the approach from DeviceManagementService using IStatusRepository to get the ID.
                var activatedStatusResult = await _statusRepository.GetStatusesByTableNameAsync(DeviceActivationRepository.TABLE_NAME);
                if (activatedStatusResult.IsFailure)
                {
                    _logger.LogError("Failed to retrieve DeviceActivation statuses for activation process. Error: {Error}", activatedStatusResult.ErrorMessage);
                    return Result<DeviceActivationResponseDto>.Failure("Service configuration error.");
                }
                var activatedStatusId = activatedStatusResult.Value.FirstOrDefault(s => s.NameStatus == DeviceActivationRepository.ACTIVATED_STATUS_NAME)?.IdStatus;

                if (!activatedStatusId.HasValue)
                {
                    _logger.LogError("Activated status ID not found in database for DeviceActivation.");
                    return Result<DeviceActivationResponseDto>.Failure("Service configuration error.");
                }


                var updateActivationResult = await _deviceActivationRepository.UpdateStatusAsync(activationEntity.IdDeviceActivation, activatedStatusId.Value, _dateTimeProvider.GetUtcNow());

                if (updateActivationResult.IsFailure)
                {
                    _logger.LogError("Failed to mark activation record {ActivationId} as activated. Error: {Error}", activationEntity.IdDeviceActivation, updateActivationResult.ErrorMessage);
                    return Result<DeviceActivationResponseDto>.Failure("Failed to complete device activation."); // Generic user message
                }


                // 4. Generate Initial Authentication Tokens
                // DeviceTokenService.GenerateAndStoreTokensAsync now returns DeviceTokenResponseDto
                var tokensResult = await _deviceTokenService.GenerateAndStoreTokensAsync(requestDto.DeviceId, ipAddress, userAgent, deviceInfo);

                if (tokensResult.IsFailure)
                {
                    _logger.LogError("Failed to generate and store tokens for Device ID {DeviceId} during activation. Error: {Error}", requestDto.DeviceId, tokensResult.ErrorMessage);
                    // Critical: device activated but no tokens issued.
                    // Consider implementing compensation logic: mark activation as error, notify admin, or attempt to revert device status.
                    return Result<DeviceActivationResponseDto>.Failure("Failed to issue authentication tokens."); // Generic user message
                }

                var deviceTokenResponseDto = tokensResult.Value; // This is the DTO we want to return.

                _logger.LogInformation("Device activation successful for Device ID: {DeviceId}. Activation Record ID: {ActivationId}.", requestDto.DeviceId, activationEntity.IdDeviceActivation);

                // Return the DeviceTokenResponseDto which has Access/Refresh tokens and expiries
                // The ActivateDeviceAsync interface returns DeviceActivationResponseDto.
                // ** ADJUSTMENT ** The return type of ActivateDeviceAsync should align with the token data returned.
                // Let's modify DeviceActivationResponseDto to include all token fields needed.
                // Or, have ActivateDeviceAsync return the same DTO as the token service: DeviceTokenResponseDto.
                // Let's make ActivateDeviceAsync return DeviceTokenResponseDto for consistency of token data.

                // --- Back to Plan Adjustment ---
                // Need to change return type of IDeviceCommunicationService.ActivateDeviceAsync to Result<DeviceTokenResponseDto>.

                // Returning the tokens DTO directly
                // ** Placeholder return based on expected new interface return type **
                // Assuming the interface is updated to Task<Result<DeviceTokenResponseDto>>
                // For now, mapping token DTO to the existing response DTO structure.
                var activationResponseDto = new DeviceActivationResponseDto // Original DTO for Activate response
                {
                    AccessToken = deviceTokenResponseDto.AccessToken,
                    RefreshToken = deviceTokenResponseDto.RefreshToken,
                    AccessTokenExpiration = deviceTokenResponseDto.AccessTokenExpiration
                    // DeviceActivationResponseDto doesn't have RefreshTokenExpiration, DeviceTokenId, DeviceId from the token service DTO.
                    // If these are needed in the activation response, DeviceActivationResponseDto must be updated.
                    // For minimal data, just tokens and access expiry might suffice for the device to start.
                };
                return Result<DeviceActivationResponseDto>.Success(activationResponseDto);


            }
            catch (InvalidOperationException ioEx) // Catch exceptions from status initialization in repositories
            {
                _logger.LogError(ioEx, "Initialization error during DeviceCommunicationService ActivateDeviceAsync.");
                return Result<DeviceActivationResponseDto>.Failure("Service initialization error."); // Generic user message
            }
            catch (DbUpdateException dbEx) // Catch DB errors not handled by repositories
            {
                _logger.LogError(dbEx, "Database error during DeviceCommunicationService ActivateDeviceAsync for Device ID {DeviceId}.", requestDto.DeviceId);
                return Result<DeviceActivationResponseDto>.Failure("A database error occurred during activation."); // Generic user message
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred during device activation for Device ID {DeviceId}.", requestDto.DeviceId);
                return Result<DeviceActivationResponseDto>.Failure("An unexpected error occurred during activation."); // Generic user message
            }
        }

        /// <inheritdoc/>
        public async Task<Result<DeviceAuthResponseDto>> AuthenticateDeviceAsync(string token, string ipAddress, string userAgent, string? deviceInfo)
        {
            _logger.LogInformation("Attempting device authentication/token validation from IP: {IPAddress}", ipAddress);

            if (string.IsNullOrWhiteSpace(token))
            {
                _logger.LogWarning("AuthenticateDeviceAsync called with null or empty token.");
                return Result<DeviceAuthResponseDto>.Failure("Token is required.");
            }

            try
            {
                // 1. Validate the token using the DeviceTokenService
                // DeviceTokenService.ValidateTokenAsync now returns DeviceTokenResponseDto
                var validationResult = await _deviceTokenService.ValidateTokenAsync(token);

                if (validationResult.IsFailure)
                {
                    _logger.LogWarning("Device token validation failed. Reason: {Error}", validationResult.ErrorMessage);
                    // DeviceTokenService.ValidateTokenAsync returns specific reasons (expired, revoked).
                    // Return the specific error message from the token service.
                    return Result<DeviceAuthResponseDto>.Failure(validationResult.ErrorMessage ?? "Invalid token.");
                }

                var deviceTokenResponseDto = validationResult.Value; // The DTO with token details


                // 2. Check Device Status (Optional business rule)
                // If we need to enforce that only 'Active' devices can authenticate for data sending:
                // Get the DeviceData for the authenticated device ID (from the token DTO).
                // Assuming deviceTokenResponseDto.DeviceId has the device ID.
                if (!deviceTokenResponseDto.DeviceId.HasValue || deviceTokenResponseDto.DeviceId.Value <= 0)
                {
                    _logger.LogError("Device token response DTO is missing Device ID during authentication.");
                    // This indicates an issue with the token service or DTO.
                    return Result<DeviceAuthResponseDto>.Failure("Internal authentication error.");
                }
                var deviceId = deviceTokenResponseDto.DeviceId.Value;

                var deviceResult = await _deviceDataRepository.GetById(deviceId);
                if (deviceResult.IsFailure || deviceResult.Value == null)
                {
                    _logger.LogError("Device with ID {DeviceId} linked to token not found during authentication. Error: {Error}", deviceId, deviceResult.ErrorMessage);
                    // This could be a sign of a data inconsistency. Log, but maybe allow authentication if the token is valid?
                    // For security, if the device record doesn't exist, authentication should probably fail.
                    return Result<DeviceAuthResponseDto>.Failure("Associated device not found.");
                }
                var deviceEntity = deviceResult.Value;


                // Need the Status ID for 'activo'. Assuming StatusRepository is injected and we can get it.
                var activeStatusResult = await _statusRepository.GetStatusesByTableNameAsync(DeviceDataRepository.TABLE_NAME);
                if (activeStatusResult.IsFailure)
                {
                    _logger.LogError("Failed to retrieve DeviceData statuses for authentication status check. Error: {Error}", activeStatusResult.ErrorMessage);
                    return Result<DeviceAuthResponseDto>.Failure("Service configuration error.");
                }
                var activeStatusId = activeStatusResult.Value.FirstOrDefault(s => s.NameStatus == DeviceDataRepository.ACTIVE_STATUS_NAME)?.IdStatus;

                if (!activeStatusId.HasValue)
                {
                    _logger.LogError("Active status ID not found in database for DeviceData.");
                    return Result<DeviceAuthResponseDto>.Failure("Service configuration error.");
                }

                // Now perform the status check:
                if (deviceEntity.StatusId.HasValue && deviceEntity.StatusId.Value != activeStatusId.Value)
                {
                    _logger.LogWarning("Device {DeviceId} authenticated but is not in Active status. Status ID: {StatusId}", deviceId, deviceEntity.StatusId.Value);
                    await LogDeviceEventAsync(deviceId, "WARNING", $"Authentication successful, but device not Active (Status ID: {deviceEntity.StatusId.Value}).");
                    // Return failure indicating the device cannot proceed
                    return Result<DeviceAuthResponseDto>.Failure("Device is not in a state to send data. Status is not Active.");
                }


                // 3. Return success with the validated token information
                // The DeviceTokenResponseDto from ValidateTokenAsync already has the tokens and expiries.
                // We need to map this to DeviceAuthResponseDto. The DTOs have similar structure.
                var responseDto = new DeviceAuthResponseDto
                {
                    AccessToken = deviceTokenResponseDto.AccessToken,
                    RefreshToken = deviceTokenResponseDto.RefreshToken,
                    AccessTokenExpiration = deviceTokenResponseDto.AccessTokenExpiration
                    // DeviceAuthResponseDto doesn't have RefreshTokenExpiration, DeviceTokenId, DeviceId from the token service DTO.
                    // If these are needed in the auth response, DeviceAuthResponseDto must be updated.
                    // For simple authentication check, just returning tokens and access expiry might be enough.
                };


                _logger.LogInformation("Device authentication successful for Device ID: {DeviceId}. Token ID: {TokenId}.", deviceId, deviceTokenResponseDto.DeviceTokenId);
                return Result<DeviceAuthResponseDto>.Success(responseDto); // Returning current tokens + expiry
            }
            catch (InvalidOperationException ioEx) // Catch exceptions from status/weather initialization in repositories/services
            {
                _logger.LogError(ioEx, "Initialization error during DeviceCommunicationService AuthenticateDeviceAsync.");
                return Result<DeviceAuthResponseDto>.Failure("Service initialization error."); // Generic user message
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred during device authentication.");
                return Result<DeviceAuthResponseDto>.Failure("An unexpected error occurred during authentication."); // Generic user message
            }
        }

        /// <inheritdoc/>
        public async Task<Result<DeviceAuthResponseDto>> RefreshDeviceTokenAsync(string refreshToken, string ipAddress, string userAgent, string? deviceInfo)
        {
            _logger.LogInformation("Refreshing device token from IP: {IPAddress}", ipAddress);

            if (string.IsNullOrWhiteSpace(refreshToken))
                return Result<DeviceAuthResponseDto>.Failure("Refresh token is required.");
            if (string.IsNullOrWhiteSpace(ipAddress) || string.IsNullOrWhiteSpace(userAgent))
                return Result<DeviceAuthResponseDto>.Failure("Client context information (IP, UserAgent) is required.");


            try
            {
                // Delegate the refresh logic to the DeviceTokenService.
                // DeviceTokenService.RefreshDeviceTokensAsync now returns DeviceTokenResponseDto.
                var refreshResult = await _deviceTokenService.RefreshDeviceTokensAsync(refreshToken, ipAddress, userAgent, deviceInfo);

                if (refreshResult.IsFailure)
                {
                    _logger.LogWarning("Device token refresh failed. Reason: {Error}", refreshResult.ErrorMessage);
                    // Propagate the specific error message from the token service.
                    return Result<DeviceAuthResponseDto>.Failure(refreshResult.ErrorMessage ?? "Token refresh failed.");
                }

                var newDeviceTokenResponseDto = refreshResult.Value; // The new tokens DTO

                // Check if the associated device is in a state to receive data (e.g., Status = Active) after refresh.
                // Get the DeviceData for the authenticated device ID (from the new token DTO).
                if (!newDeviceTokenResponseDto.DeviceId.HasValue || newDeviceTokenResponseDto.DeviceId.Value <= 0)
                {
                    _logger.LogError("New device token response DTO is missing Device ID during refresh.");
                    // This indicates an issue with the token service or DTO after refresh.
                    return Result<DeviceAuthResponseDto>.Failure("Internal authentication error after refresh.");
                }
                var deviceId = newDeviceTokenResponseDto.DeviceId.Value;

                var deviceResult = await _deviceDataRepository.GetById(deviceId);
                if (deviceResult.IsFailure || deviceResult.Value == null)
                {
                    _logger.LogError("Device with ID {DeviceId} linked to new token not found during refresh status check. Error: {Error}", deviceId, deviceResult.ErrorMessage);
                    return Result<DeviceAuthResponseDto>.Failure("Associated device not found after refresh.");
                }
                var deviceEntity = deviceResult.Value;

                // Check device status
                var activeStatusResult = await _statusRepository.GetStatusesByTableNameAsync(DeviceDataRepository.TABLE_NAME);
                if (activeStatusResult.IsFailure)
                {
                    _logger.LogError("Failed to retrieve DeviceData statuses for refresh status check. Error: {Error}", activeStatusResult.ErrorMessage);
                    return Result<DeviceAuthResponseDto>.Failure("Service configuration error.");
                }
                var activeStatusId = activeStatusResult.Value.FirstOrDefault(s => s.NameStatus == DeviceDataRepository.ACTIVE_STATUS_NAME)?.IdStatus;

                if (!activeStatusId.HasValue)
                {
                    _logger.LogError("Active status ID not found in database for DeviceData.");
                    return Result<DeviceAuthResponseDto>.Failure("Service configuration error.");
                }

                if (deviceEntity.StatusId.HasValue && deviceEntity.StatusId.Value != activeStatusId.Value)
                {
                    _logger.LogWarning("Device {DeviceId} refreshed token but is not in Active status. Status ID: {StatusId}", deviceId, deviceEntity.StatusId.Value);
                    await LogDeviceEventAsync(deviceId, "WARNING", $"Token refresh successful, but device not Active (Status ID: {deviceEntity.StatusId.Value}).");
                    // Return failure indicating the device cannot proceed after refresh
                    return Result<DeviceAuthResponseDto>.Failure("Device is not in a state to send data. Status is not Active after refresh.");
                }


                // Return the NEW tokens (already in the response DTO format).
                // Map DeviceTokenResponseDto to DeviceAuthResponseDto.
                var responseDto = new DeviceAuthResponseDto
                {
                    AccessToken = newDeviceTokenResponseDto.AccessToken,
                    RefreshToken = newDeviceTokenResponseDto.RefreshToken,
                    AccessTokenExpiration = newDeviceTokenResponseDto.AccessTokenExpiration
                    // DeviceAuthResponseDto doesn't have RefreshTokenExpiration, DeviceTokenId, DeviceId.
                };


                _logger.LogInformation("Device token refresh successful for Device ID: {DeviceId}. New Token ID: {NewTokenId}.", deviceId, newDeviceTokenResponseDto.DeviceTokenId);
                return Result<DeviceAuthResponseDto>.Success(responseDto);

            }
            catch (InvalidOperationException ioEx) // Catch exceptions from status/weather initialization in repositories/services
            {
                _logger.LogError(ioEx, "Initialization error during DeviceCommunicationService RefreshDeviceTokenAsync.");
                return Result<DeviceAuthResponseDto>.Failure("Service initialization error."); // Generic user message
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred during device token refresh.");
                return Result<DeviceAuthResponseDto>.Failure("An unexpected error occurred during token refresh."); // Generic user message
            }
        }

        /// <inheritdoc/>
        public async Task<Result> ReceiveAmbientDataAsync(int deviceId, AmbientDataDto dataDto)
        {
            _logger.LogInformation("Receiving ambient data for Device ID: {DeviceId}", deviceId);

            if (deviceId <= 0)
            {
                _logger.LogWarning("ReceiveAmbientDataAsync called with invalid device ID: {DeviceId}", deviceId);
                return Result.Failure("Invalid device ID.");
            }
            if (dataDto == null)
            {
                _logger.LogWarning("ReceiveAmbientDataAsync called with null DTO for Device ID: {DeviceId}", deviceId);
                return Result.Failure("Ambient data cannot be null.");
            }
            // DTO validation attributes handle basic format/required checks on dataDto.


            try
            {
                // 1. Get Device Data to determine PlantId and CropId
                var deviceResult = await _deviceDataRepository.GetById(deviceId);
                if (deviceResult.IsFailure)
                {
                    _logger.LogError("Device with ID {DeviceId} not found when receiving ambient data. Error: {Error}", deviceId, deviceResult.ErrorMessage);
                    await LogDeviceEventAsync(deviceId, "ERROR", "Received ambient data, but device not found.");
                    return Result.Failure("Device not found.");
                }
                var deviceEntity = deviceResult.Value;

                // 2. Validate Device Association (PlantId and CropId must be non-null as per rule)
                if (!deviceEntity.PlantId.HasValue || !deviceEntity.CropId.HasValue)
                {
                    _logger.LogWarning("Received ambient data for Device ID {DeviceId}, but device is not associated with a Plant or Crop (PlantId: {PlantId}, CropId: {CropId}).", deviceId, deviceEntity.PlantId, deviceEntity.CropId);
                    await LogDeviceEventAsync(deviceId, "WARNING", "Received ambient data from unassociated device.");
                    return Result.Failure("Device is not associated with a plant or crop.");
                }

                int plantId = deviceEntity.PlantId.Value;
                int cropId = deviceEntity.CropId.Value;


                // 3. Get City Weather Data for City Temperature and Humidity
                // Need Crop location details. Fetch Crop by CropId.
                var cropResult = await _cropRepository.GetById(cropId); // Requires ICropRepository.GetById
                if (cropResult.IsFailure || cropResult.Value == null)
                {
                    _logger.LogError("Crop with ID {CropId} not found for Device ID {DeviceId} when receiving ambient data. Error: {Error}", cropId, deviceId, cropResult.ErrorMessage);
                    await LogDeviceEventAsync(deviceId, "ERROR", $"Received ambient data, but associated Crop ID {cropId} not found.");
                    return Result.Failure("Associated crop data not found.");
                }
                var cropEntity = cropResult.Value;

                // Assuming CropEntity.CityName contains "city, state, country"
                var locationParts = cropEntity.CityName.Split(','); // Split "city, state, country"
                var cityApi = locationParts.Length > 0 ? locationParts[0].Trim() : string.Empty;
                var stateProvinceApi = locationParts.Length > 1 ? locationParts[1].Trim() : null; // Nullable state/province
                var countryApi = locationParts.Length > 2 ? locationParts[2].Trim() : string.Empty;

                if (string.IsNullOrWhiteSpace(cityApi) || string.IsNullOrWhiteSpace(countryApi))
                {
                    _logger.LogError("Crop ID {CropId} has invalid location format in CityName: {CityName}", cropId, cropEntity.CityName);
                    await LogDeviceEventAsync(deviceId, "ERROR", $"Received ambient data, but associated Crop location format invalid ({cropEntity.CityName}).");
                    return Result.Failure("Associated crop location data is invalid.");
                }


                var weatherResult = await _weatherService.GetCityWeatherAsync(cityApi, stateProvinceApi, countryApi);

                if (weatherResult.IsFailure)
                {
                    _logger.LogError("Failed to get city weather data for {City} when receiving ambient data from Device {DeviceId}. Error: {Error}", cropEntity.CityName, deviceId, weatherResult.ErrorMessage);
                    await LogDeviceEventAsync(deviceId, "ERROR", $"Received ambient data, but failed to get city weather ({cropEntity.CityName}).");
                    return Result.Failure("Failed to retrieve city weather data.");
                }
                var cityWeather = weatherResult.Value;

                // 4. Map AmbientDataDto to SensorData Entity
                var now = _dateTimeProvider.GetUtcNow(); // Use backend time for RecordedAt
                var sensorDataEntity = dataDto.ToSensorDataEntity(
                    plantId: plantId, // Use validated PlantId
                    cropId: cropId, // Use validated CropId
                    recordedAt: now // Use backend timestamp
                );

                // Assign city weather data to the entity
                sensorDataEntity.CityTemperature = cityWeather.TemperatureC; 
                sensorDataEntity.CityHumidity = cityWeather.Humidity;


                // 5. Save Sensor Data
                var saveResult = await _sensorDataRepository.Create(sensorDataEntity);

                if (saveResult.IsFailure)
                {
                    _logger.LogError("Failed to save ambient data for Device ID {DeviceId}, Plant ID {PlantId}. Error: {Error}", deviceId, plantId, saveResult.ErrorMessage);
                    await LogDeviceEventAsync(deviceId, "ERROR", "Failed to save ambient data.");
                    return Result.Failure("Failed to save ambient data.");
                }

                _logger.LogInformation("Successfully received and saved ambient data for Device ID {DeviceId}, Plant ID {PlantId}. SensorData ID: {SensorDataId}", deviceId, plantId, saveResult.Value.IdSensorData);
                return Result.Success();
            }
            catch (InvalidOperationException ioEx) // Catch exceptions from status/weather initialization in repositories/services
            {
                _logger.LogError(ioEx, "Initialization error during DeviceCommunicationService ReceiveAmbientDataAsync.");
                return Result.Failure("Service initialization error."); // Generic user message
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while receiving ambient data for Device ID {DeviceId}.", deviceId);
                await LogDeviceEventAsync(deviceId, "ERROR", "An unexpected error occurred while processing ambient data.");
                return Result.Failure("An unexpected error occurred while processing ambient data."); // Generic user message
            }
        }

        /// <inheritdoc/>
        // Added parameter string thermalImageDataJson
        public async Task<Result> ReceiveCaptureDataAsync(int deviceId, ThermalDataDto thermalDataDto, IFormFile imageFile, DateTime recordedAt, string thermalImageDataJson)
        {
            _logger.LogInformation("Receiving capture data for Device ID: {DeviceId}", deviceId);

            if (deviceId <= 0)
            {
                _logger.LogWarning("ReceiveCaptureDataAsync called with invalid device ID: {DeviceId}", deviceId);
                return Result.Failure("Invalid device ID.");
            }
            if (thermalDataDto == null)
            {
                _logger.LogWarning("ReceiveCaptureDataAsync called with null thermal data DTO for Device ID: {DeviceId}", deviceId);
                return Result.Failure("Thermal data cannot be null.");
            }
            if (imageFile == null || imageFile.Length == 0)
            {
                _logger.LogWarning("ReceiveCaptureDataAsync called with null or empty image file for Device ID: {DeviceId}", deviceId);
                return Result.Failure("Image file cannot be null or empty.");
            }
            if (string.IsNullOrWhiteSpace(thermalImageDataJson))
            {
                _logger.LogWarning("ReceiveCaptureDataAsync called with null or empty thermal image JSON string for Device ID: {DeviceId}", deviceId);
                return Result.Failure("Thermal image JSON data cannot be null or empty.");
            }

            // DTO validation attributes handle basic checks on thermalDataDto.

            // Optional: Validate thermalDataDto.Temperatures array size
            if (thermalDataDto.Temperatures == null || thermalDataDto.Temperatures.Length != 768) // Assuming THERMAL_PIXELS is 768
            {
                _logger.LogWarning("Received thermal data for Device ID {DeviceId} with unexpected temperatures array size: {Size}. Expected 768.", deviceId, thermalDataDto.Temperatures?.Length ?? 0);
                await LogDeviceEventAsync(deviceId, "WARNING", $"Received thermal data with unexpected array size ({thermalDataDto.Temperatures?.Length ?? 0}).");
            }


            try
            {
                // 1. Get Device Data to determine PlantId and CropId
                var deviceResult = await _deviceDataRepository.GetById(deviceId);
                if (deviceResult.IsFailure)
                {
                    _logger.LogError("Device with ID {DeviceId} not found when receiving capture data. Error: {Error}", deviceId, deviceResult.ErrorMessage);
                    await LogDeviceEventAsync(deviceId, "ERROR", "Received capture data, but device not found.");
                    return Result.Failure("Device not found.");
                }
                var deviceEntity = deviceResult.Value;

                // 2. Validate Device Association (PlantId and CropId must be non-null as per rule)
                if (!deviceEntity.PlantId.HasValue || !deviceEntity.CropId.HasValue)
                {
                    _logger.LogWarning("Received capture data for Device ID {DeviceId}, but device is not associated with a Plant or Crop (PlantId: {PlantId}, CropId: {CropId}).", deviceId, deviceEntity.PlantId, deviceEntity.CropId);
                    await LogDeviceEventAsync(deviceId, "WARNING", "Received capture data from unassociated device.");
                    return Result.Failure("Device is not associated with a plant or crop.");
                }

                int plantId = deviceEntity.PlantId.Value;
                int cropId = deviceEntity.CropId.Value;

                // 3. Determine if it's Night based on Crop Location and Current Time
                // Need Crop location details. Fetch Crop by CropId.
                var cropResult = await _cropRepository.GetById(cropId); // Requires ICropRepository.GetById
                if (cropResult.IsFailure || cropResult.Value == null)
                {
                    _logger.LogError("Crop with ID {CropId} not found for Device ID {DeviceId} when receiving capture data. Error: {Error}", cropId, deviceId, cropResult.ErrorMessage);
                    await LogDeviceEventAsync(deviceId, "ERROR", $"Received capture data, but associated Crop ID {cropId} not found.");
                    return Result.Failure("Associated crop data not found.");
                }
                var cropEntity = cropResult.Value;

                // Assuming CropEntity.CityName contains "city, state, country"
                var locationParts = cropEntity.CityName.Split(',');
                var cityApi = locationParts.Length > 0 ? locationParts[0].Trim() : string.Empty;
                var stateProvinceApi = locationParts.Length > 1 ? locationParts[1].Trim() : null; // Nullable
                var countryApi = locationParts.Length > 2 ? locationParts[2].Trim() : string.Empty;

                if (string.IsNullOrWhiteSpace(cityApi) || string.IsNullOrWhiteSpace(countryApi))
                {
                    _logger.LogError("Crop ID {CropId} has invalid location format in CityName: {CityName}", cropId, cropEntity.CityName);
                    await LogDeviceEventAsync(deviceId, "ERROR", $"Received capture data, but associated Crop location format invalid ({cropEntity.CityName}).");
                    return Result.Failure("Associated crop location data is invalid.");
                }


                var weatherResult = await _weatherService.GetCityWeatherAsync(cityApi, stateProvinceApi, countryApi);

                if (weatherResult.IsFailure)
                {
                    _logger.LogError("Failed to get city weather data for {City} when receiving capture data from Device {DeviceId}. Error: {Error}", cropEntity.CityName, deviceId, weatherResult.ErrorMessage);
                    await LogDeviceEventAsync(deviceId, "ERROR", $"Received capture data, but failed to get city weather ({cropEntity.CityName}).");
                    return Result.Failure("Failed to retrieve city weather data.");
                }
                var cityWeather = weatherResult.Value;

                // Determine if it is day based on the API's 'is_day' indicator
                bool isDaytime = cityWeather.IsDay;


                // 4. Process Image File and Thermal Data
                byte[]? rgbImageData = null;

                if (isDaytime)
                {
                    // Read the image file into a byte array only if it's daytime
                    rgbImageData = await ReadFileBytesAsync(imageFile);

                    _logger.LogDebug("Processed RGB image for Device ID {DeviceId}, Plant ID {PlantId} (Daytime). Image size: {Size} bytes.", deviceId, plantId, rgbImageData?.Length);
                }
                else
                {
                    _logger.LogInformation("Skipping RGB image storage for Device ID {DeviceId}, Plant ID {PlantId} as it is nighttime in {City}.", deviceId, plantId, cityApi);
                    await LogDeviceEventAsync(deviceId, "INFO", "Skipped RGB image storage (nighttime).");
                }

                // Use the original JSON string received from the controller/device for thermal data.
                string thermalImageDataJsonToSave = thermalImageDataJson;


                // 5. Map to ThermalData Entity
                var now = _dateTimeProvider.GetUtcNow(); // Use backend time for RecordedAt

                // Pass byte array if daytime, null if nighttime to entity constructor.
                var thermalDataEntityToSave = thermalDataDto.ToThermalDataEntity(
                    rgbImageData: rgbImageData, // Pass the byte array (can be null)
                    thermalImageDataJson: thermalImageDataJsonToSave, // Pass the raw JSON string
                    plantId: plantId,
                    cropId: cropId,
                    recordedAt: now // Use backend timestamp
                );


                // 6. Save Thermal Data
                var saveResult = await _thermalDataRepository.Create(thermalDataEntityToSave);

                if (saveResult.IsFailure)
                {
                    _logger.LogError("Failed to save thermal data for Device ID {DeviceId}, Plant ID {PlantId}. Error: {Error}", deviceId, plantId, saveResult.ErrorMessage);
                    await LogDeviceEventAsync(deviceId, "ERROR", "Failed to save thermal data.");
                    return Result.Failure("Failed to save thermal data.");
                }

                _logger.LogInformation("Successfully received and saved thermal data for Device ID {DeviceId}, Plant ID {PlantId}. ThermalData ID: {ThermalDataId}", deviceId, plantId, saveResult.Value.IdThermalData);
                return Result.Success();

            }
            catch (InvalidOperationException ioEx) // Catch exceptions from status/weather initialization in repositories/services
            {
                _logger.LogError(ioEx, "Initialization error during DeviceCommunicationService ReceiveCaptureDataAsync.");
                return Result.Failure("Service initialization error."); // Generic user message
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while receiving capture data for Device ID {DeviceId}.", deviceId);
                await LogDeviceEventAsync(deviceId, "ERROR", "An unexpected error occurred while processing capture data.");
                return Result.Failure("An unexpected error occurred while processing capture data."); // Generic user message
            }
        }

        /// <summary>
        /// Helper method to read IFormFile content into a byte array.
        /// Returns null if the file is null or empty.
        /// </summary>
        /// <param name="file">The IFormFile to read.</param>
        /// <returns>A byte array containing the file content, or null if the file is null or empty.</returns>
        private static async Task<byte[]?> ReadFileBytesAsync(IFormFile? file) // Made nullable
        {
            if (file == null || file.Length == 0)
            {
                return null;
            }

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            return ms.ToArray();
        }


        /// <inheritdoc/>
        public async Task<Result> ReceiveDeviceLogAsync(int deviceId, DeviceLogEntryDto logEntryDto)
        {
            _logger.LogInformation("Receiving device log for Device ID: {DeviceId}, Type: {LogType}", deviceId, logEntryDto?.LogType);

            if (deviceId <= 0)
            {
                _logger.LogWarning("ReceiveDeviceLogAsync called with invalid device ID: {DeviceId}", deviceId);
                return Result.Failure("Invalid device ID.");
            }
            if (logEntryDto == null)
            {
                _logger.LogWarning("ReceiveDeviceLogAsync called with null DTO for Device ID: {DeviceId}", deviceId);
                return Result.Failure("Log entry data cannot be null.");
            }
            // DTO validation attributes handle basic format/required checks on logEntryDto.

            try
            {
                // 1. Map DTO to DeviceLog Entity
                var logEntity = logEntryDto.ToDeviceLogEntity(deviceId);

                // 2. Save Device Log
                var saveResult = await _deviceLogRepository.Create(logEntity);

                if (saveResult.IsFailure)
                {
                    _logger.LogError("Failed to save device log for Device ID {DeviceId}, Type {LogType}. Error: {Error}", deviceId, logEntryDto.LogType, saveResult.ErrorMessage);
                    return Result.Failure("Failed to save device log."); // Generic user message
                }

                _logger.LogInformation("Successfully received and saved device log for Device ID {DeviceId}, Type {LogType}. DeviceLog ID: {DeviceLogId}", deviceId, logEntity.LogType, saveResult.Value.IdDeviceLog);
                return Result.Success();
            }
            catch (InvalidOperationException ioEx) // Catch exceptions from status initialization in repositories
            {
                _logger.LogError(ioEx, "Initialization error during DeviceCommunicationService ReceiveDeviceLogAsync.");
                return Result.Failure("Service initialization error."); // Generic user message
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while receiving device log for Device ID {DeviceId}.", deviceId);
                return Result.Failure("An unexpected error occurred while receiving device log."); // Generic user message
            }
        }

        /// <summary>
        /// Helper method to log an event specifically in the DeviceLog table.
        /// Uses the DeviceLogRepository.
        /// </summary>
        /// <param name="deviceId">The ID of the device the log is for.</param>
        /// <param name="logType">The type of log (e.g., INFO, WARNING, ERROR).</param>
        /// <param name="message">The log message content.</param>
        /// <returns>A Task representing the asynchronous logging operation.</returns>
        private async Task LogDeviceEventAsync(int deviceId, string logType, string message)
        {
            try
            {
                var logEntity = new DeviceLogEntity(
                    idDeviceLog: 0, // DB generates ID
                    deviceId: deviceId,
                    logType: logType,
                    logMessage: message,
                    logTimestamp: _dateTimeProvider.GetUtcNow() // Backend timestamp
                );

                var result = await _deviceLogRepository.Create(logEntity);

                if (result.IsFailure)
                {
                    _logger.LogError("Failed to log device event to DeviceLog table for Device ID {DeviceId}. Type: {LogType}, Message: {Message}. Repository Error: {Error}",
                                    deviceId, logType, message, result.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred in LogDeviceEventAsync for Device ID {DeviceId}.", deviceId);
            }
            // Fire-and-forget
        }
    }
}