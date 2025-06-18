using ArandanoIRT_Backend.Application.DTOs.Device;
using ArandanoIRT_Backend.Application.Interfaces.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ArandanoIRT_Backend.UI.Controllers
{
    /// <summary>
    /// Handles API endpoints related to the management of device (camera) records.
    /// Accessible by authenticated users, with administrator role required for modification operations.
    /// </summary>
    [Route("api/devices")] 
    [ApiController] 
    [Authorize] 
    public class DeviceManagementController : ControllerBase
    {
        private readonly IDeviceManagementService _deviceManagementService;
        private readonly ILogger<DeviceManagementController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DeviceManagementController"/> class.
        /// </summary>
        /// <param name="deviceManagementService">The application service for device management logic.</param>
        /// <param name="logger">The logger for this controller.</param>
        /// <exception cref="ArgumentNullException">Thrown if dependencies are null.</exception>
        public DeviceManagementController(
            IDeviceManagementService deviceManagementService,
            ILogger<DeviceManagementController> logger)
        {
            _deviceManagementService = deviceManagementService ?? throw new ArgumentNullException(nameof(deviceManagementService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Creates a new device record.
        /// Requires Administrator role.
        /// </summary>
        /// <param name="createDto">Data for the new device.</param>
        /// <returns>An IActionResult indicating success (200 OK with creation details) or failure (400 Bad Request).</returns>
        /// <response code="200">Device created successfully.</response>
        /// <response code="400">Invalid data provided.</response>
        /// <response code="401">Authentication failed.</response>
        /// <response code="403">User is not authorized to create devices (missing Admin role).</response>
        [HttpPost] 
        [Authorize(Roles = "Admin")] 
        [ProducesResponseType(typeof(CreateDeviceResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> CreateDevice([FromBody] CreateDeviceDto createDto)
        {
            _logger.LogInformation("Received request to create device by user.");

            // Get the ID of the authenticated user performing the action
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int registeredByUserId))
            {
                _logger.LogError("Authenticated user ID claim missing or invalid during device creation.");
                // This should ideally not happen if authentication is working correctly, but defensive check.
                return Unauthorized("Could not identify authenticated user.");
            }

            // Optional: Explicit ModelState check if needed beyond [ApiController] automatic checks.
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for CreateDevice request. Errors: {@ModelState}", ModelState.Values.SelectMany(v => v.Errors));
                return BadRequest(ModelState);
            }


            // Call the application service
            var result = await _deviceManagementService.CreateDeviceAsync(createDto, registeredByUserId);

            // Handle the service result
            if (result.IsSuccess)
            {
                _logger.LogInformation("Device created successfully by user {UserId}. Device ID: {DeviceId}", registeredByUserId, result.Value.DeviceDetails.IdDeviceData);
                // Return 200 OK with the response DTO containing creation details
                return Ok(result.Value); // Assuming CreateDeviceResponseDto is the value
            }
            else
            {
                _logger.LogWarning("Device creation failed for user {UserId}. Reason: {Error}", registeredByUserId, result.ErrorMessage);
                return BadRequest(result.ErrorMessage); // Generic error message from service
            }
        }

        /// <summary>
        /// Retrieves a list of all device records.
        /// Accessible by all authenticated users.
        /// </summary>
        /// <returns>A collection of basic device DTOs.</returns>
        /// <response code="200">List of devices retrieved successfully.</response>
        /// <response code="401">Authentication failed.</response>
        /// <response code="400">An error occurred.</response>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<DeviceDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetAllDevices()
        {
            _logger.LogInformation("Received request to get all devices.");

            // Optional: Get authenticated user ID if filtering by user/crop is implemented later.
            // var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Call the application service
            var result = await _deviceManagementService.GetAllDevicesAsync();

            // Handle the service result
            if (result.IsSuccess)
            {
                _logger.LogInformation("Successfully retrieved {Count} devices.", result.Value?.Count() ?? 0);
                return Ok(result.Value); // Return the collection of DTOs
            }
            else
            {
                _logger.LogWarning("Failed to retrieve all devices. Reason: {Error}", result.ErrorMessage);
                return BadRequest(result.ErrorMessage); // Generic error message from service
            }
        }

        /// <summary>
        /// Retrieves detailed LogInformation for a specific device by its ID.
        /// Accessible by all authenticated users.
        /// </summary>
        /// <param name="id">The ID of the device to retrieve.</param>
        /// <returns>A detailed device DTO.</returns>
        /// <response code="200">Device details retrieved successfully.</response>
        /// <response code="404">Device not found.</response>
        /// <response code="401">Authentication failed.</response>
        /// <response code="400">An error occurred.</response>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(DeviceDetailDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetDeviceById(int id)
        {
            _logger.LogInformation("Received request to get device details for ID: {DeviceId}", id);

            if (id <= 0)
            {
                _logger.LogWarning("GetDeviceById called with invalid ID: {DeviceId}", id);
                return BadRequest("Invalid device ID format.");
            }

            // Call the application service
            var result = await _deviceManagementService.GetDeviceByIdAsync(id);

            // Handle the service result
            if (result.IsSuccess)
            {
                if (result.Value == null)
                {
                    _logger.LogInformation("Device with ID {DeviceId} not found by service.", id);
                    return NotFound("Device not found."); // Specific 404 if service indicates not found
                }
                _logger.LogInformation("Successfully retrieved details for device ID: {DeviceId}.", id);
                return Ok(result.Value); // Return the detailed DTO
            }
            else
            {
                // Service might return failure if not found or other error
                if (result.ErrorMessage?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true)
                {
                    _logger.LogInformation("Service reported device ID {DeviceId} not found.", id);
                    return NotFound("Device not found."); // Map service "not found" to 404
                }
                _logger.LogWarning("Failed to retrieve device details for ID {DeviceId}. Reason: {Error}", id, result.ErrorMessage);
                return BadRequest(result.ErrorMessage); // Generic error message from service
            }
        }

        /// <summary>
        /// Updates an existing device record.
        /// Requires Administrator role.
        /// </summary>
        /// <param name="id">The ID of the device to update.</param>
        /// <param name="updateDto">Data for the device update.</param>
        /// <returns>An IActionResult indicating success (204 No Content) or failure (400 Bad Request).</returns>
        /// <response code="204">Device updated successfully.</response>
        /// <response code="400">Invalid data provided or an error occurred.</response>
        /// <response code="404">Device not found.</response>
        /// <response code="401">Authentication failed.</response>
        /// <response code="403">User is not authorized to update devices (missing Admin role).</response>
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")] 
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> UpdateDevice(int id, [FromBody] UpdateDeviceDto updateDto)
        {
            _logger.LogInformation("Received request to update device ID: {DeviceId} by user.", id);

            if (id <= 0)
            {
                _logger.LogWarning("UpdateDevice called with invalid ID: {DeviceId}", id);
                return BadRequest("Invalid device ID format.");
            }
            if (updateDto == null)
            {
                _logger.LogWarning("UpdateDevice called with null DTO for ID: {DeviceId}", id);
                return BadRequest("Device data cannot be null for update.");
            }
            // DTO validation attributes handle basic checks on updateDto.
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for UpdateDevice request (ID: {DeviceId}). Errors: {@ModelState}", id, ModelState.Values.SelectMany(v => v.Errors));
                return BadRequest(ModelState);
            }


            // Get the ID of the authenticated user performing the action
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int updatedByUserId))
            {
                _logger.LogError("Authenticated user ID claim missing or invalid during device update for ID {DeviceId}.", id);
                return Unauthorized("Could not identify authenticated user.");
            }


            // Call the application service
            var result = await _deviceManagementService.UpdateDeviceAsync(id, updateDto, updatedByUserId);

            // Handle the service result
            if (result.IsSuccess)
            {
                if (result.Value == false) // Service returns Success(false) if device not found or no changes
                {
                    _logger.LogInformation("Service reported device ID {DeviceId} not found for update or no changes were needed.", id);
                    // If service explicitly signals not found (e.g., by returning false when not found), map to 404.
                    // Assuming service returns false when device not found OR when no changes were saved.
                    // Better practice: service returns a specific Result.NotFound() or includes a 'NotFound' status in Result.
                    // Based on our current service result, let's map service error messages containing "not found" to 404.
                    if (result.ErrorMessage?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        return NotFound("Device not found for update.");
                    }
                    // If Value is false but no "not found" error, it means no changes were saved (still a success from API perspective).
                    _logger.LogInformation("Device update operation for ID {DeviceId} completed with no changes saved.", id);
                    return NoContent(); // 204 No Content indicates success with no content in response body.
                }

                _logger.LogInformation("Device ID {DeviceId} updated successfully by user {UserId}.", id, updatedByUserId);
                return NoContent(); // 204 No Content for successful update

            }
            else
            {
                // Service returns failure for validation errors, DB errors, etc.
                if (result.ErrorMessage?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true)
                {
                    _logger.LogWarning("Service reported device ID {DeviceId} not found for update. Reason: {Error}", id, result.ErrorMessage);
                    return NotFound("Device not found for update."); // Map service "not found" to 404
                }
                _logger.LogWarning("Device update failed for ID {DeviceId} by user {UserId}. Reason: {Error}", id, updatedByUserId, result.ErrorMessage);
                return BadRequest(result.ErrorMessage); // Generic error message from service
            }
        }

        /// <summary>
        /// Deletes a device record.
        /// Requires Administrator role.
        /// </summary>
        /// <param name="id">The ID of the device to delete.</param>
        /// <returns>An IActionResult indicating success (204 No Content) or failure (400 Bad Request).</returns>
        /// <response code="204">Device deleted successfully.</response>
        /// <response code="400">An error occurred during deletion (e.g., related records exist).</response>
        /// <response code="404">Device not found.</response>
        /// <response code="401">Authentication failed.</response>
        /// <response code="403">User is not authorized to delete devices (missing Admin role).</response>
        [HttpDelete("{id}")] 
        [Authorize(Roles = "Admin")] 
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> DeleteDevice(int id)
        {
            _logger.LogInformation("Received request to delete device ID: {DeviceId} by user.", id);

            if (id <= 0)
            {
                _logger.LogWarning("DeleteDevice called with invalid ID: {DeviceId}", id);
                return BadRequest("Invalid device ID format.");
            }

            // Optional: Get authenticated user ID for logging/auditing if needed by service.
            // var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);


            // Call the application service
            var result = await _deviceManagementService.DeleteDeviceAsync(id);

            // Handle the service result
            if (result.IsSuccess)
            {
                // Service returns Success(true) if deletion occurred, Failure if not found or error.
                if (result.Value == false) // Service returns Success(false) if device not found for deletion
                {
                    _logger.LogInformation("Service reported device ID {DeviceId} not found for deletion.", id);
                    return NotFound("Device not found for deletion."); // Map service "not found" to 404
                }
                _logger.LogInformation("Device ID {DeviceId} deleted successfully.", id);
                return NoContent(); // 204 No Content for successful deletion
            }
            else
            {
                // Service returns failure for not found, DB errors, etc.
                if (result.ErrorMessage?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true)
                {
                    _logger.LogWarning("Service reported device ID {DeviceId} not found for deletion. Reason: {Error}", id, result.ErrorMessage);
                    return NotFound("Device not found for deletion."); // Map service "not found" to 404
                }
                // Check for specific error messages from service (e.g., related records prevent deletion)
                // if (result.ErrorMessage?.Contains("related records", StringComparison.OrdinalIgnoreCase) == true)
                // {
                //      _logger.LogWarning("Deletion failed for device ID {DeviceId} due to related records. Reason: {Error}", id, result.ErrorMessage);
                //      return BadRequest($"Cannot delete device ID {id}. It has associated data (logs, tokens, etc.). Please delete related data first or contact support.");
                // }

                _logger.LogWarning("Device deletion failed for ID {DeviceId}. Reason: {Error}", id, result.ErrorMessage);
                return BadRequest(result.ErrorMessage); // Generic error message from service
            }
        }
    }
}
