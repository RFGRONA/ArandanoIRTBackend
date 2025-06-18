using ArandanoIRT_Backend.Application.DTOs.Device;
using ArandanoIRT_Backend.Domain.ValueObjects;

namespace ArandanoIRT_Backend.Application.Interfaces.IServices
{
    /// <summary>
    /// Defines the interface for the service responsible for managing device (camera) records.
    /// Includes CRUD operations, primarily intended for administrators, and read operations for users.
    /// </summary>
    public interface IDeviceManagementService
    {
        /// <summary>
        /// Creates a new device record and generates its initial activation code.
        /// This operation is typically restricted to administrators.
        /// </summary>
        /// <param name="createDto">The DTO containing data for the new device.</param>
        /// <param name="registeredByUserId">The ID of the user creating the device.</param>
        /// <returns>
        /// A <see cref="Result{T}"/> indicating success and the created <see cref="CreateDeviceResponseDto"/>
        /// including the generated activation code and ID, or failure with an error message.
        /// </returns>
        Task<Result<CreateDeviceResponseDto>> CreateDeviceAsync(CreateDeviceDto createDto, int registeredByUserId);

        /// <summary>
        /// Retrieves a list of all device records.
        /// Can be accessed by both administrators and regular users (potentially with filtering by crop).
        /// </summary>
        /// <returns>
        /// A <see cref="Result{T}"/> indicating success and a collection of <see cref="DeviceDto"/>,
        /// or failure with an error message.
        /// </returns>
        Task<Result<IEnumerable<DeviceDto>>> GetAllDevicesAsync();

        /// <summary>
        /// Retrieves detailed information for a specific device record by its ID.
        /// Can be accessed by both administrators and regular users.
        /// </summary>
        /// <param name="deviceId">The ID of the device to retrieve.</param>
        /// <returns>
        /// A <see cref="Result{T}"/> indicating success and the <see cref="DeviceDetailDto"/>,
        /// or failure if the device is not found or an error occurs.
        /// </returns>
        Task<Result<DeviceDetailDto>> GetDeviceByIdAsync(int deviceId);

        /// <summary>
        /// Updates an existing device record.
        /// This operation is typically restricted to administrators.
        /// </summary>
        /// <param name="deviceId">The ID of the device to update.</param>
        /// <param name="updateDto">The DTO containing the updated data.</param>
        /// <param name="updatedByUserId">The ID of the user updating the device.</param>
        /// <returns>
        /// A <see cref="Result"/> indicating success or failure with an error message.
        /// Returns Success(true) if changes were saved, Success(false) if no changes were needed.
        /// </returns>
        Task<Result<bool>> UpdateDeviceAsync(int deviceId, UpdateDeviceDto updateDto, int updatedByUserId);

        /// <summary>
        /// Deletes a device record and associated data (logs, tokens, activation records).
        /// This operation is typically restricted to administrators.
        /// </summary>
        /// <param name="deviceId">The ID of the device to delete.</param>
        /// <returns>
        /// A <see cref="Result"/> indicating success or failure with an error message.
        /// Returns Success(true) if deletion occurred, Failure if not found or error.
        /// </returns>
        Task<Result<bool>> DeleteDeviceAsync(int deviceId);
    }
}