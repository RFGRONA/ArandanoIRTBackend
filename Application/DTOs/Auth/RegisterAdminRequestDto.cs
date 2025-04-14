using ArandanoIRT_Backend.Application.DTOs.Objetcts; // <- This line remains uncommented as requested.

namespace ArandanoIRT_Backend.Application.DTOs.Auth
{
    /// <summary>
    /// Represents the personal and credential information for an administrator user.
    /// </summary>
    public class AdminInfoDto
    {
        /// <summary>
        /// Gets or sets the administrator's first name.
        /// </summary>
        public required string FirstName { get; set; }

        /// <summary>
        /// Gets or sets the administrator's last name.
        /// </summary>
        public required string LastName { get; set; }

        /// <summary>
        /// Gets or sets the administrator's email address, typically used for login.
        /// </summary>
        public required string Email { get; set; }

        /// <summary>
        /// Gets or sets the administrator's initial password for the account.
        /// </summary>
        public required string Password { get; set; }
    }

    /// <summary>
    /// Represents the request data for registering a new administrator account
    /// along with associated crop information.
    /// </summary>
    public class RegisterAdminRequestDto
    {
        /// <summary>
        /// Gets or sets the administrator's personal and credential information.
        /// </summary>
        /// <seealso cref="AdminInfoDto"/>
        public required AdminInfoDto AdminInfo { get; set; }

        /// <summary>
        /// Gets or sets the information about the crop associated with this new administrator.
        /// </summary>
        /// <seealso cref="CropInfoDto"/> // Assuming CropInfoDto is defined in the referenced Objects namespace
        public required CropInfoDto CropInfo { get; set; }

        /// <summary>
        /// Gets or sets the optional CAPTCHA verification token provided during the registration process.
        /// </summary>
        public string? CaptchaToken { get; set; }
    }
}