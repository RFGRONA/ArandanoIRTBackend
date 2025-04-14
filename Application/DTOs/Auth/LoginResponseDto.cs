namespace ArandanoIRT_Backend.Application.DTOs.Auth
{
    /// <summary>
    /// Represents the data returned after a successful user authentication.
    /// </summary>
    public class LoginResponseDto
    {
        /// <summary>
        /// Gets or sets the authentication token (e.g., JWT) issued for the session.
        /// </summary>
        public required string Token { get; set; }

        /// <summary>
        /// Gets or sets the username of the authenticated user.
        /// </summary>
        public required string Username { get; set; }

        /// <summary>
        /// Gets or sets the email address of the authenticated user.
        /// </summary>
        public required string Email { get; set; }

        /// <summary>
        /// Gets or sets the role assigned to the authenticated user (e.g., "Admin", "User").
        /// </summary>
        public required string Role { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier of the authenticated user.
        /// </summary>
        public required int IdUser { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier of the crop associated with the authenticated user.
        /// </summary>
        /// <remarks>
        /// This identifier likely links the user to a specific agricultural crop or related entity within the application domain.
        /// </remarks>
        public required int IdCrop { get; set; }
    }
}