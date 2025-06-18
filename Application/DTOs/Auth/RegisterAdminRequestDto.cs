using ArandanoIRT_Backend.Application.DTOs.Objects;
using ArandanoIRT_Backend.Infrastructure.Attributes;
using System.ComponentModel.DataAnnotations; // <- This line remains uncommented as requested.

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
        /// <example>string</example> 
        [Required]
        [StringLength(40, ErrorMessage = "First name cannot exceed 40 characters.")]
        [RegularExpression(@"^[a-zA-Z\s]+$", ErrorMessage = "Last name can only contain letters and spaces.")]
        [Display(Name = "First Name")]
        [DataType(DataType.Text)]
        [SanitizeHtml]
        public required string FirstName { get; set; }

        /// <summary>
        /// Gets or sets the administrator's last name.
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
        /// Gets or sets the administrator's email address, typically used for login.
        /// </summary>
        [Required]
        [EmailAddress(ErrorMessage = "Invalid email address format.")]
        [StringLength(75, ErrorMessage = "Email address cannot exceed 75 characters.")]
        [DataType(DataType.EmailAddress)]
        [SanitizeHtml]
        public required string Email { get; set; }

        /// <summary>
        /// Gets or sets the administrator's initial password for the account.
        /// </summary>
        /// <example>string</example> 
        [Required]
        [DataType(DataType.Password)]
        [SanitizeHtml]
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