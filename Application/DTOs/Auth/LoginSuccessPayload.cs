namespace ArandanoIRT_Backend.Application.DTOs.Auth
{
    /// <summary>
    /// Represents the data returned after a successful login,
    /// including user details, access token, and the refresh token string.
    /// </summary>
    public class LoginSuccessPayload
    {
        /// <summary>
        /// Standard login response details (user info, access token).
        /// </summary>
        public required LoginResponseDto LoginDetails { get; set; }

        /// <summary>
        /// The opaque refresh token string generated for this session.
        /// This should NOT typically be returned in the API response body,
        /// but is needed by the controller to set the HttpOnly cookie.
        /// </summary>
        public required string RefreshToken { get; set; }
    }
}