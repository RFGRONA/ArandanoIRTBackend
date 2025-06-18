using ArandanoIRT_Backend.Application.DTOs.Device;
using ArandanoIRT_Backend.Domain.ValueObjects;

namespace ArandanoIRT_Backend.Application.Interfaces.IServices
{
    /// <summary>
    /// Defines the interface for the service responsible for handling direct communication with devices.
    /// Includes operations for activation, authentication, and receiving data/logs.
    /// </summary>
    public interface IDeviceCommunicationService
    {
        /// <summary>
        /// Handles a device's request to activate itself using an activation code.
        /// Validates the code and provides initial authentication tokens upon success.
        /// </summary>
        /// <param name="requestDto">The DTO containing the device ID and activation code.</param>
        /// <param name="ipAddress">The IP address of the device.</param>
        /// <param name="userAgent">The user agent string from the device's request.</param>
        /// <param name="deviceInfo">Formatted device information string.</param>
        /// <returns>
        /// A <see cref="Result{T}"/> indicating success and the <see cref="DeviceActivationResponseDto"/>
        /// containing tokens, or failure with an error message (e.g., invalid code, device not found).
        /// </returns>
        Task<Result<DeviceActivationResponseDto>> ActivateDeviceAsync(DeviceActivationRequestDto requestDto, string ipAddress, string userAgent, string? deviceInfo);

        /// <summary>
        /// Authenticates a device using its current token (access token) and potentially
        /// issues a new token if nearing expiry (token refresh logic).
        /// Also serves as an endpoint for devices to check backend status/authentication validity.
        /// </summary>
        /// <param name="token">The access token provided by the device.</param>
        /// <param name="ipAddress">The IP address of the device.</param>
        /// <param name="userAgent">The user agent string from the device's request.</param>
        /// <param name="deviceInfo">Formatted device information string.</param>
        /// <returns>
        /// A <see cref="Result{T}"/> indicating success and the <see cref="DeviceAuthResponseDto"/>
        /// containing the current or a new token pair, or failure with an error message (e.g., invalid/expired token).
        /// </returns>
        Task<Result<DeviceAuthResponseDto>> AuthenticateDeviceAsync(string token, string ipAddress, string userAgent, string? deviceInfo);

        /// <summary>
        /// Refreshes a device's authentication tokens using a valid refresh token.
        /// </summary>
        /// <param name="refreshToken">The refresh token provided by the device.</param>
        /// <param name="ipAddress">The IP address of the device.</param>
        /// <param name="userAgent">The user agent string from the device's request.</param>
        /// <param name="deviceInfo">Formatted device information string.</param>
        /// <returns>
        /// A <see cref="Result{T}"/> indicating success and a new <see cref="DeviceAuthResponseDto"/>
        /// containing updated tokens, or failure if the refresh token is invalid/expired/revoked.
        /// </returns>
        Task<Result<DeviceAuthResponseDto>> RefreshDeviceTokenAsync(string refreshToken, string ipAddress, string userAgent, string? deviceInfo);


        /// <summary>
        /// Receives and processes environmental data sent by a device.
        /// Requires device authentication.
        /// </summary>
        /// <param name="deviceId">The ID of the authenticated device sending the data (extracted from token).</param>
        /// <param name="dataDto">The DTO containing the environmental sensor readings.</param>
        /// <returns>A <see cref="Result"/> indicating success or failure.</returns>
        Task<Result> ReceiveAmbientDataAsync(int deviceId, AmbientDataDto dataDto);

        /// <summary>
        /// Receives and processes thermal and image data sent by a device.
        /// Requires device authentication. Includes logic to handle RGB image based on time.
        /// </summary>
        /// <param name="deviceId">The ID of the authenticated device sending the data (extracted from token).</param>
        /// <param name="thermalDataDto">The DTO containing the thermal data and statistics (JSON part).</param>
        /// <param name="imageFile">The image file (RGB/Thermal) received via multipart form.</param>
        /// <param name="recordedAt">The timestamp when the data was recorded on the device (important for validation).</param>
        /// <param name="thermalImageDataJson">The original JSON string of the thermal image data received from the device.</param> // Added parameter
        /// <returns>A <see cref="Result"/> indicating success or failure.</returns>
        Task<Result> ReceiveCaptureDataAsync(int deviceId, ThermalDataDto thermalDataDto, IFormFile imageFile, DateTime recordedAt, string thermalImageDataJson);


        /// <summary>
        /// Receives and processes a log entry sent by a device.
        /// Requires device authentication.
        /// </summary>
        /// <param name="deviceId">The ID of the authenticated device sending the log (extracted from token).</param>
        /// <param name="logEntryDto">The DTO containing the log information.</param>
        /// <returns>A <see cref="Result"/> indicating success or failure.</returns>
        Task<Result> ReceiveDeviceLogAsync(int deviceId, DeviceLogEntryDto logEntryDto);
    }
}