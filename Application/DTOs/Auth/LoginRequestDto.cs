using ArandanoIRT_Backend.Infrastructure.Attributes;
using System.ComponentModel.DataAnnotations;

namespace ArandanoIRT_Backend.Application.DTOs.Auth
{
    /// <summary>
    /// Represents the request data for a user login attempt.
    /// </summary>
    public class LoginRequestDto
    {
        /// <summary>
        /// Gets or sets the user's email address.
        /// </summary>
        [Required]
        [EmailAddress(ErrorMessage = "Invalid email address format.")]
        [StringLength(75, ErrorMessage = "Email address cannot exceed 75 characters.")]
        [DataType(DataType.EmailAddress)]
        [SanitizeHtml]
        public required string Email { get; set; }

        /// <summary>
        /// Gets or sets the user's password.
        /// </summary>
        /// <example>string</example> 
        [Required]
        [DataType(DataType.Password)]
        [SanitizeHtml]
        public required string Password { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the user wishes the login session to be persistent (e.g., "remember me").
        /// Defaults to <c>false</c>.
        /// </summary>
        public bool RememberMe { get; set; } = false;

        /// <summary>
        /// Gets or sets the optional CAPTCHA verification token provided by the user.
        /// This may be required depending on security configurations or login attempt history.
        /// </summary>
        public string? CaptchaToken { get; set; }
    }
}