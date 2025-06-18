using ArandanoIRT_Backend.Application.DTOs.Device; 
using ArandanoIRT_Backend.Application.Interfaces.IServices; 
using ArandanoIRT_Backend.Application.Interfaces.Utilities; 
using ArandanoIRT_Backend.Application.Mappings; 
using ArandanoIRT_Backend.Domain.Entities; 
using ArandanoIRT_Backend.Domain.IRepositories; 
using ArandanoIRT_Backend.Domain.ValueObjects; 
using Microsoft.EntityFrameworkCore;
using ArandanoIRT_Backend.Infrastructure.Repositories;
using ArandanoIRT_Backend.Application.Utilities;

namespace ArandanoIRT_Backend.Application.Services
{
    /// <summary>
    /// Implements the <see cref="IDeviceManagementService"/> interface, providing application logic
    /// for managing device (camera) records.
    /// </summary>
    public class DeviceManagementService : IDeviceManagementService
    {
        private readonly IDeviceDataRepository _deviceDataRepository;
        private readonly IDeviceActivationRepository _deviceActivationRepository;
        private readonly IStatusRepository _statusRepository; 
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly ILogger<DeviceManagementService> _logger;

        // Configuration for activation code expiry (e.g., 24 hours)
        private readonly TimeSpan _activationCodeExpiry;


        /// <summary>
        /// Initializes a new instance of the <see cref="DeviceManagementService"/> class.
        /// </summary>
        /// <param name="deviceDataRepository">The repository for device data.</param>
        /// <param name="deviceActivationRepository">The repository for device activation records.</param>
        /// <param name="statusRepository">The repository for status data (to get names/IDs).</param>
        /// <param name="dateTimeProvider">The provider for current date and time.</param>
        /// <param name="logger">The logger instance.</param>
        /// <param name="configuration">The application configuration.</param>
        /// <exception cref="ArgumentNullException">Thrown if any dependency is null.</exception>
        public DeviceManagementService(
            IDeviceDataRepository deviceDataRepository,
            IDeviceActivationRepository deviceActivationRepository,
            IStatusRepository statusRepository,
            IDateTimeProvider dateTimeProvider,
            ILogger<DeviceManagementService> logger,
            IConfiguration configuration
            )
        {
            _deviceDataRepository = deviceDataRepository ?? throw new ArgumentNullException(nameof(deviceDataRepository));
            _deviceActivationRepository = deviceActivationRepository ?? throw new ArgumentNullException(nameof(deviceActivationRepository));
            _statusRepository = statusRepository ?? throw new ArgumentNullException(nameof(statusRepository)); 
            _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            if (!TimeSpan.TryParse(configuration["DeviceSettings:ActivationCodeExpiry"], out _activationCodeExpiry) || _activationCodeExpiry <= TimeSpan.Zero)
            {
                _logger.LogWarning("DeviceSettings:ActivationCodeExpiry configuration is missing or invalid. Using default 24 hours.");
                _activationCodeExpiry = TimeSpan.FromHours(24);
            }
            _logger.LogInformation("DeviceManagementService initialized with Activation Code expiry: {Expiry}", _activationCodeExpiry);
        }


        /// <inheritdoc/>
        public async Task<Result<CreateDeviceResponseDto>> CreateDeviceAsync(CreateDeviceDto createDto, int registeredByUserId)
        {
            _logger.LogInformation("Attempting to create new device: {DeviceName} by user ID: {RegisteredByUserId}", createDto?.NameDevice, registeredByUserId);

            if (createDto == null)
            {
                _logger.LogWarning("CreateDeviceAsync called with null DTO.");
                return Result<CreateDeviceResponseDto>.Failure("Device data cannot be null.");
            }
            // DTO validation attributes handle basic format/required checks before reaching here.
            if (createDto.PlantId.HasValue && !createDto.CropId.HasValue)
            {
                _logger.LogWarning("CreateDeviceAsync called with PlantId but no CropId for device {DeviceName}.", createDto.NameDevice);
                return Result<CreateDeviceResponseDto>.Failure("Plant must be associated with a Crop.");
            }

            try
            {
                var now = _dateTimeProvider.GetUtcNow();

                // 1. Map DTO to DeviceData Entity
                var deviceEntity = createDto.ToDeviceDataEntity(now, registeredByUserId);

                // 2. Generate Activation Code and Expiry
                var activationCode = ActivationCodeGenerator.GenerateActivationCode();
                var activationExpiresAt = now.Add(_activationCodeExpiry);

                // 3. Create DeviceData record (Repository handles DB ID generation and sets default status)
                var deviceCreateResult = await _deviceDataRepository.Create(deviceEntity);

                if (deviceCreateResult.IsFailure)
                {
                    _logger.LogError("Failed to create device in repository. Error: {Error}", deviceCreateResult.ErrorMessage);
                    return Result<CreateDeviceResponseDto>.Failure("Failed to save device data."); 
                }

                var createdDeviceEntity = deviceCreateResult.Value;

                // 4. Create DeviceActivation record
                var activationEntity = new DeviceActivationEntity(
                    idDeviceActivation: 0, // DB will generate ID
                    deviceId: createdDeviceEntity.IdDeviceData, // Use the generated Device ID
                    activationCode: activationCode,
                    createdAt: now,
                    expiresAt: activationExpiresAt,
                    activationStatus: null // Repository will default this to 'Pending'
                );

                var activationCreateResult = await _deviceActivationRepository.Create(activationEntity);

                if (activationCreateResult.IsFailure)
                {
                    _logger.LogCritical("Device ID {DeviceId} created successfully, but failed to create activation record. Error: {Error}",
                                         createdDeviceEntity.IdDeviceData, activationCreateResult.ErrorMessage);
                    return Result<CreateDeviceResponseDto>.Failure("Failed to generate activation code for the device.");
                }

                var createdActivationEntity = activationCreateResult.Value;

                // 5. Fetch related data needed for the response DTO (DeviceDetailDto part)
                var fetchDetailResult = await _deviceDataRepository.GetById(createdDeviceEntity.IdDeviceData);

                if (fetchDetailResult.IsFailure)
                {
                    _logger.LogError("Failed to re-fetch newly created device {DeviceId} with related data for response DTO. Error: {Error}", createdDeviceEntity.IdDeviceData, fetchDetailResult.ErrorMessage);
                    return Result<CreateDeviceResponseDto>.Failure("Device created, but failed to retrieve details for confirmation.");
                }
                var deviceDetailEntity = fetchDetailResult.Value;

                // 6. Map fetched entity to DeviceDetailDto
                var deviceDetailDto = deviceDetailEntity.ToDeviceDetailDto(
                     statusName: deviceDetailEntity.StatusName,
                     registeredByName: deviceDetailEntity.RegisteredByName,
                     updatedByName: deviceDetailEntity.UpdatedByName,
                     cropName: deviceDetailEntity.CropName,
                     plantName: deviceDetailEntity.PlantName
                );

                // 7. Construct the final CreateDeviceResponseDto
                var createResponseDto = new CreateDeviceResponseDto
                {
                    DeviceDetails = deviceDetailDto, // Include the populated detail DTO
                    ActivationCode = createdActivationEntity.ActivationCode,
                    ActivationId = createdActivationEntity.IdDeviceActivation
                };

                _logger.LogInformation("Device creation successful. Device ID: {DeviceId}, Activation ID: {ActivationId}.", createdDeviceEntity.IdDeviceData, createdActivationEntity.IdDeviceActivation);
                return Result<CreateDeviceResponseDto>.Success(createResponseDto);

            }
            catch (InvalidOperationException ioEx) // Catch exceptions from status initialization in repositories
            {
                _logger.LogError(ioEx, "Initialization error during DeviceManagementService CreateDeviceAsync.");
                return Result<CreateDeviceResponseDto>.Failure("Service initialization error.");
            }
            catch (DbUpdateException dbEx) // Catch DB errors not handled by repositories (less likely with current structure)
            {
                _logger.LogError(dbEx, "Database error during DeviceManagementService CreateDeviceAsync.");
                return Result<CreateDeviceResponseDto>.Failure("A database error occurred.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred during device creation.");
                return Result<CreateDeviceResponseDto>.Failure("An unexpected error occurred.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<IEnumerable<DeviceDto>>> GetAllDevicesAsync()
        {
            _logger.LogInformation("Attempting to retrieve all devices for list view.");
            try
            {
                // 1. Get all DeviceData entities, including related names (Status, Crop)
                var devicesResult = await _deviceDataRepository.GetAll();
                if (devicesResult.IsFailure)
                {
                    _logger.LogError("Failed to retrieve all devices from repository. Error: {Error}", devicesResult.ErrorMessage);
                    return Result<IEnumerable<DeviceDto>>.Failure("Error retrieving devices.");
                }

                var deviceEntities = devicesResult.Value.ToList();

                // 2. Map entities to DTOs
                // The entities now contain StatusName and CropName due to repository Includes and mapping.
                var deviceDtos = deviceEntities.Select(entity => entity.ToDeviceDto(
                    statusName: entity.StatusName, 
                    cropName: entity.CropName 
                )).ToList();


                _logger.LogInformation("Successfully retrieved and mapped {Count} devices to DTOs for list view.", deviceDtos.Count);
                return Result<IEnumerable<DeviceDto>>.Success(deviceDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while retrieving all devices.");
                return Result<IEnumerable<DeviceDto>>.Failure("An error occurred while retrieving devices.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<DeviceDetailDto>> GetDeviceByIdAsync(int deviceId)
        {
            _logger.LogInformation("Attempting to retrieve device details for ID: {DeviceId}", deviceId);

            if (deviceId <= 0)
            {
                _logger.LogWarning("GetDeviceByIdAsync called with invalid device ID: {DeviceId}", deviceId);
                return Result<DeviceDetailDto>.Failure("Invalid device ID provided.");
            }

            try
            {
                // 1. Get the DeviceData entity, including all related names
                var deviceResult = await _deviceDataRepository.GetById(deviceId);
                if (deviceResult.IsFailure)
                {
                    _logger.LogWarning("Device with ID {DeviceId} not found in repository. Error: {Error}", deviceId, deviceResult.ErrorMessage);
                    return Result<DeviceDetailDto>.Failure("Device not found."); // Generic user message
                }
                var deviceEntity = deviceResult.Value;

                // 2. Map entity to detailed DTO
                // The entity now contains all *Name properties due to repository's Include and mapping.
                var detailDto = deviceEntity.ToDeviceDetailDto(
                    statusName: deviceEntity.StatusName, 
                    registeredByName: deviceEntity.RegisteredByName, 
                    updatedByName: deviceEntity.UpdatedByName, 
                    cropName: deviceEntity.CropName, 
                    plantName: deviceEntity.PlantName 
                );

                _logger.LogInformation("Successfully retrieved device details for ID: {DeviceId}.", deviceId);
                return Result<DeviceDetailDto>.Success(detailDto);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while retrieving device details.");
                return Result<DeviceDetailDto>.Failure("An error occurred while retrieving device details.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<bool>> UpdateDeviceAsync(int deviceId, UpdateDeviceDto updateDto, int updatedByUserId)
        {
            _logger.LogInformation("Attempting to update device ID: {DeviceId} by user ID: {UpdatedByUserId}", deviceId, updatedByUserId);

            if (deviceId <= 0)
            {
                _logger.LogWarning("UpdateDeviceAsync called with invalid device ID: {DeviceId}", deviceId);
                return Result<bool>.Failure("Invalid device ID provided for update.");
            }
            if (updateDto == null)
            {
                _logger.LogWarning("UpdateDeviceAsync called with null DTO for device ID: {DeviceId}", deviceId);
                return Result<bool>.Failure("Device data cannot be null for update.");
            }
            // DTO validation attributes handle basic format/required checks on updateDto.
            if (updateDto.StatusId.HasValue)
            {
                // Need to validate if updateDto.StatusId is a valid status ID for DeviceData.
                var deviceStatusesResult = await _statusRepository.GetStatusesByTableNameAsync(DeviceDataRepository.TABLE_NAME);

                if (deviceStatusesResult.IsFailure)
                {
                    _logger.LogError("Failed to retrieve device statuses from repository for validation. Error: {Error}", deviceStatusesResult.ErrorMessage);
                    return Result<bool>.Failure("Error validating device status.");
                }

                var validStatusIds = deviceStatusesResult.Value.Select(s => s.IdStatus).ToList();

                if (!validStatusIds.Contains(updateDto.StatusId.Value))
                {
                    _logger.LogWarning("Attempted to update device {DeviceId} with invalid status ID: {StatusId}. Valid IDs: {ValidIds}", deviceId, updateDto.StatusId.Value, string.Join(",", validStatusIds));
                    return Result<bool>.Failure("Invalid status ID provided for device.");
                }
            }

            // Optional: Validate CropId and PlantId existence/validity if changed.
            if (updateDto.PlantId.HasValue && !updateDto.CropId.HasValue)
            {
                _logger.LogWarning("UpdateDeviceAsync called with PlantId but no CropId for device {DeviceId}.", deviceId);
                return Result<bool>.Failure("Plant must be associated with a Crop.");
            }

            try
            {
                // 1. Get the existing DeviceData entity (tracked by repository's FindAsync via Update method)
                var deviceResult = await _deviceDataRepository.GetById(deviceId); 
                if (deviceResult.IsFailure)
                {
                    _logger.LogWarning("Device with ID {DeviceId} not found for update logic. Error: {Error}", deviceId, deviceResult.ErrorMessage);
                    return Result<bool>.Failure("Device not found for update.");
                }
                var existingDeviceEntity = deviceResult.Value;


                var now = _dateTimeProvider.GetUtcNow();

                // 2. Map updated data from DTO onto the existing domain entity
                updateDto.MapToDeviceDataEntity(existingDeviceEntity, now, updatedByUserId);

                // 3. Call repository to update the entity
                var updateResult = await _deviceDataRepository.Update(existingDeviceEntity);

                if (updateResult.IsFailure)
                {
                    _logger.LogError("Failed to update device ID {DeviceId} in repository. Error: {Error}", deviceId, updateResult.ErrorMessage);
                    return Result<bool>.Failure("Failed to save device data."); 
                }

                if (updateResult.Value)
                {
                    _logger.LogInformation("Successfully updated device ID: {DeviceId}.", deviceId);
                }
                else
                {
                    _logger.LogInformation("Update operation for device ID {DeviceId} completed with no changes saved.", deviceId);
                }

                return Result<bool>.Success(updateResult.Value);

            }
            catch (InvalidOperationException ioEx) // Catch exceptions from status initialization in repositories/StatusRepository
            {
                _logger.LogError(ioEx, "Initialization error during DeviceManagementService UpdateDeviceAsync.");
                return Result<bool>.Failure("Service initialization error.");
            }
            catch (DbUpdateException dbEx) // Catch DB errors not handled by repositories
            {
                _logger.LogError(dbEx, "Database error during DeviceManagementService UpdateDeviceAsync.");
                return Result<bool>.Failure("A database error occurred.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred during device update.");
                return Result<bool>.Failure("An unexpected error occurred.");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<bool>> DeleteDeviceAsync(int deviceId)
        {
            _logger.LogInformation("Attempting to delete device ID: {DeviceId}", deviceId);

            if (deviceId <= 0)
            {
                _logger.LogWarning("DeleteDeviceAsync called with invalid device ID: {DeviceId}", deviceId);
                return Result<bool>.Failure("Invalid device ID provided for deletion.");
            }

            try
            {
                // Call the repository to delete the device and its related entities within a transaction.
                var deleteResult = await _deviceDataRepository.Delete(deviceId);

                if (deleteResult.IsFailure)
                {
                    _logger.LogError("Failed to delete device ID {DeviceId} from repository. Error: {Error}", deviceId, deleteResult.ErrorMessage);
                    return Result<bool>.Failure(deleteResult.ErrorMessage ?? "Failed to delete the device.");
                }

                if (deleteResult.Value)
                {
                    _logger.LogInformation("Successfully deleted device ID: {DeviceId}.", deviceId);
                    return Result<bool>.Success(true);
                }
                else
                {
                    _logger.LogWarning("Delete operation for device ID {DeviceId} completed, but repository indicated no deletion occurred.", deviceId);
                    // This case should ideally correspond to "Device not found for deletion" from the repository.
                    return Result<bool>.Failure("Device not found for deletion.");
                }

            }
            catch (InvalidOperationException ioEx) // Catch exceptions from status initialization in repositories
            {
                _logger.LogError(ioEx, "Initialization error during DeviceManagementService DeleteDeviceAsync.");
                return Result<bool>.Failure("Service initialization error.");
            }
            catch (DbUpdateException dbEx) // Catch DB errors not handled by repositories
            {
                _logger.LogError(dbEx, "Database error during DeviceManagementService DeleteDeviceAsync.");
                return Result<bool>.Failure("A database error occurred during deletion.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred during device deletion.");
                return Result<bool>.Failure("An unexpected error occurred.");
            }
        }
    }
}