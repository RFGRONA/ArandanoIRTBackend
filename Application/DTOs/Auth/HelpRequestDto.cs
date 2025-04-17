using ArandanoIRT_Backend.Infrastructure.Attributes;
using System.ComponentModel.DataAnnotations;

namespace ArandanoIRT_Backend.Application.DTOs.Auth
{
    /// <summary>
    /// Represents the data submitted through a help or support request form.
    /// </summary>
    public class HelpRequestDto
    {
        /// <summary>
        /// Gets or sets the name of the person submitting the help request.
        /// </summary>
        /// <example>string</example> //
        [Required]
        [StringLength(80, ErrorMessage = "Name cannot exceed 80 characters.")]
        [RegularExpression(@"^[a-zA-Z\s]+$", ErrorMessage = "Last name can only contain letters and spaces.")]
        [Display(Name = "Name")]
        [DataType(DataType.Text)]
        [SanitizeHtml] 
        public required string Name { get; set; }

        /// <summary>
        /// Gets or sets the email address of the person submitting the help request.
        /// This is typically used for follow-up communication.
        /// </summary>
        [Required]
        [EmailAddress(ErrorMessage = "Invalid email address format.")]
        [StringLength(75, ErrorMessage = "Email address cannot exceed 75 characters.")]
        [Display(Name = "Email")]
        [DataType(DataType.EmailAddress)]
        [SanitizeHtml] 
        public required string Email { get; set; }

        /// <summary>
        /// Gets or sets the subject line of the help request.
        /// </summary>
        /// <example>string</example> //
        [Required]
        [StringLength(150, ErrorMessage = "Subject cannot exceed 150 characters.")]
        [Display(Name = "Subject")]
        [DataType(DataType.Text)]
        [RegularExpression(@"^[a-zA-Z\s]+$", ErrorMessage = "Subject can only contain letters and spaces.")]
        [SanitizeHtml] 
        public required string Subject { get; set; }

        /// <summary>
        /// Gets or sets the name of the crop related to the help request.
        /// </summary>
        [Required]
        [StringLength(100, ErrorMessage = "Crop name cannot exceed 100 characters.")]
        [Display(Name = "Crop Name")]
        [DataType(DataType.Text)]
        [SanitizeHtml] 
        public required string CropName { get; set; }

        /// <summary>
        /// Gets or sets the detailed message or description of the issue or question provided by the user.
        /// </summary>
        [Required]
        [StringLength(240, ErrorMessage = "Message cannot exceed 240 characters.")]
        [Display(Name = "Message")]
        [DataType(DataType.MultilineText)] 
        [SanitizeHtml] 
        public required string Message { get; set; }

        /// <summary>
        /// Gets or sets the optional CAPTCHA verification token provided by the user.
        /// This may be required depending on security configurations or the nature of the request.
        /// </summary>
        public string? CaptchaToken { get; set; }
    }
}
