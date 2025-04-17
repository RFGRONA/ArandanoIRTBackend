using ArandanoIRT_Backend.Infrastructure.Attributes;
using System.ComponentModel.DataAnnotations;

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
        /// <example>string</example> 
        [Required]
        [StringLength(40, ErrorMessage = "First name cannot exceed 40 characters.")]
        [RegularExpression(@"^[a-zA-Z\s]+$", ErrorMessage = "Last name can only contain letters and spaces.")]
        [Display(Name = "First Name")]
        [DataType(DataType.Text)]
        [SanitizeHtml]
        public required string FirstName { get; set; }

        /// <summary>
        /// Gets or sets the user's last name.
        /// </summary>
        /// <example>string</example> 
        [Required]
        [StringLength(40, ErrorMessage = "Last Name name cannot exceed 40 characters.")]
        [RegularExpression(@"^[a-zA-Z\s]+$", ErrorMessage = "Last name can only contain letters and spaces.")]
        [Display(Name = "Last Name")]
        [DataType(DataType.Text)]
        [SanitizeHtml]
        public required string LastName { get; set; }

        /// <summary>
        /// Gets or sets the user's email address, typically used for login.
        /// </summary>
        [Required]
        [EmailAddress(ErrorMessage = "Invalid email address format.")]
        [StringLength(75, ErrorMessage = "Email address cannot exceed 75 characters.")]
        [DataType(DataType.EmailAddress)]
        [SanitizeHtml]
        public required string Email { get; set; }

        /// <summary>
        /// Gets or sets the user's initial password for the account.
        /// </summary>
        /// /// <example>string</example> 
        [Required]
        [DataType(DataType.Password)]
        [SanitizeHtml]
        public required string Password { get; set; }

        /// <summary>
        /// Gets or sets the authorization code required for user registration.
        /// This code might be provided by an administrator or through an invitation process.
        /// </summary>
        /// <example>string</example> 
        [Required]
        [StringLength(10, ErrorMessage = "Authorization code cannot exceed 10 characters.")]
        [RegularExpression(@"^[a-zA-Z0-9]+$", ErrorMessage = "Authorization code can only contain letters and numbers.")]
        [Display(Name = "Authorization Code")]
        [DataType(DataType.Text)]
        [SanitizeHtml]
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