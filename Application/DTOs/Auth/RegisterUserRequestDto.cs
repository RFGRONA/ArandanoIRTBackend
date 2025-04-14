namespace ArandanoIRT_Backend.Application.DTOs.Auth
{
    /// <summary>
    /// Represents the personal and credential information for a standard user during registration.
    /// </summary>
    public class UserInfoDto
    {
        /// <summary>
        /// Gets or sets the user's first name.
        /// </summary>
        public required string FirstName { get; set; }

        /// <summary>
        /// Gets or sets the user's last name.
        /// </summary>
        public required string LastName { get; set; }

        /// <summary>
        /// Gets or sets the user's email address, typically used for login.
        /// </summary>
        public required string Email { get; set; }

        /// <summary>
        /// Gets or sets the user's initial password for the account.
        /// </summary>
        public required string Password { get; set; }

        /// <summary>
        /// Gets or sets the authorization code required for user registration.
        /// This code might be provided by an administrator or through an invitation process.
        /// </summary>
        public required string AuthCode { get; set; }
    }

    /// <summary>
    /// Represents the request data for registering a new standard user account.
    /// </summary>
    public class RegisterUserRequestDto
    {
        /// <summary>
        /// Gets or sets the user's personal and credential information.
        /// </summary>
        /// <seealso cref="UserInfoDto"/>
        public required UserInfoDto UserInfo { get; set; }

        /// <summary>
        /// Gets or sets the optional CAPTCHA verification token provided during the registration process.
        /// </summary>
        public string? CaptchaToken { get; set; }
    }
}