namespace ArandanoIRT_Backend.Application.DTOs.Auth
{
    /// <summary>
    /// Represents the response containing authentication tokens (access and refresh).
    /// </summary>
    public class TokenResponseDto
    {
        /// <summary>
        /// Gets or sets the access token used for authorizing API requests.
        /// This token typically has a short lifespan.
        /// </summary>
        public required string AccessToken { get; set; }

        /// <summary>
        /// Gets or sets the refresh token used to obtain a new access token
        /// without requiring the user to re-authenticate.
        /// This token typically has a longer lifespan than the access token.
        /// </summary>
        public required string RefreshToken { get; set; }

        /// <summary>
        /// Gets or sets the exact date and time (usually UTC) when the <see cref="AccessToken"/> expires.
        /// </summary>
        public DateTime AccessTokenExpiration { get; set; }
    }
}