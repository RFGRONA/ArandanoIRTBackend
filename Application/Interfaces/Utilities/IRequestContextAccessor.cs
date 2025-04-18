using ArandanoIRT_Backend.Domain.ValueObjects; 

namespace ArandanoIRT_Backend.Application.Interfaces.Utilities
{
    /// <summary>
    /// Defines a contract for accessing information related to the current HTTP request context,
    /// such as client details and authenticated user information.
    /// </summary>
    public interface IRequestContextAccessor
    {
        /// <summary>
        /// Gets the IP address of the client making the current request.
        /// </summary>
        /// <returns>The client's IP address as a string. May return an empty string or placeholder if unavailable.</returns>
        string GetIpAddress();

        /// <summary>
        /// Gets the User-Agent string sent by the client in the current request header.
        /// </summary>
        /// <returns>The client's User-Agent string. May return an empty string if unavailable.</returns>
        string GetUserAgent();

        /// <summary>
        /// Gets structured information about the client (e.g., browser, OS, device)
        /// typically parsed from the User-Agent string.
        /// </summary>
        /// <returns>A <see cref="ClientInfo"/> object containing parsed client details.</returns>
        ClientInfo GetClientInfo(); // Assumes ClientInfo is defined in ArandanoIRT_Backend.Domain.ValueObjects

        /// <summary>
        /// Gets a formatted string summarizing the client's device and browser information.
        /// </summary>
        /// <returns>A human-readable string describing the client's device and software.</returns>
        string GetFormattedDeviceInfo();

        /// <summary>
        /// Gets the unique identifier of the currently authenticated user associated with the request.
        /// </summary>
        /// <returns>The user's ID as an integer, or <c>null</c> if the user is not authenticated or the ID cannot be determined.</returns>
        int? GetCurrentUserId();

        /// <summary>
        /// Gets the unique identifier of the crop associated with the current request context
        /// (e.g., based on the authenticated user or other request parameters).
        /// </summary>
        /// <returns>The crop's ID as an integer, or <c>null</c> if no specific crop is associated with the context or it's unavailable.</returns>
        int? GetCurrentCropId();
    }
}