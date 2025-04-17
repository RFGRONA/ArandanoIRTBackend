using ArandanoIRT_Backend.Infrastructure.Attributes;
using System.ComponentModel.DataAnnotations;

namespace ArandanoIRT_Backend.Application.DTOs.Auth
{
    /// <summary>
    /// Represents the request data for initiating the password recovery process.
    /// </summary>
    public class ForgotPasswordRequestDto
    {
        /// <summary>
        /// Gets or sets the email address of the user requesting the password reset.
        /// </summary>
        [Required]
        [EmailAddress(ErrorMessage = "Invalid email address format.")]
        [StringLength(75, ErrorMessage = "Email address cannot exceed 75 characters.")]
        [DataType(DataType.EmailAddress)]
        [SanitizeHtml]
        public required string Email { get; set; }

        /// <summary>
        /// Gets or sets the optional CAPTCHA verification token provided by the user.
        /// This may be required depending on security configurations.
        /// </summary>
        public string? CaptchaToken { get; set; }
    }
}