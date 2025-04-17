using ArandanoIRT_Backend.Infrastructure.Attributes;
using System.ComponentModel.DataAnnotations;

namespace ArandanoIRT_Backend.Application.DTOs.Auth
{
    /// <summary>
    /// Represents the request data for changing a user's password using a verification code.
    /// </summary>
    public class ChangePasswordRequestDto
    {
        /// <summary>
        /// Gets or sets the unique code required to authorize the password change.
        /// This code is typically sent to the user via email or another secure channel.
        /// </summary>
        [Required]
        [StringLength(10, MinimumLength = 12, ErrorMessage = "The change code must contain between 12 and 10 characters")]
        [DataType(DataType.Text)]
        [SanitizeHtml]
        public required string ChangeCode { get; set; }

        /// <summary>
        /// Gets or sets the desired new password for the user account.
        /// </summary>
        /// <example>string</example> 
        [Required]
        [DataType(DataType.Password)]
        [SanitizeHtml]
        public required string NewPassword { get; set; }

        /// <summary>
        /// Gets or sets the confirmation of the new password.
        /// This field is used to ensure the user typed the new password correctly.
        /// </summary>
        /// <example>string</example> 
        [Required]
        [DataType(DataType.Password)]
        [SanitizeHtml]
        public required string ConfirmPassword { get; set; }
    }
}