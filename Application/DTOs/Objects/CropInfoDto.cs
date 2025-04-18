using ArandanoIRT_Backend.Infrastructure.Attributes;
using System.ComponentModel.DataAnnotations;

namespace ArandanoIRT_Backend.Application.DTOs.Objetcts 
{
    /// <summary>
    /// Represents basic information about a specific crop.
    /// </summary>
    public class CropInfoDto
    {
        /// <summary>
        /// Gets or sets the name assigned to the crop.
        /// </summary>
        /// <example>string</example> 
        [Required]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "The crop name must contain between 3 and 50 characters")]
        [RegularExpression(@"^[a-zA-Z0-9\s]+$", ErrorMessage = "The crop name can only contain letters, numbers, and spaces.")]
        [Display(Name = "Crop Name")]
        [DataType(DataType.Text)]
        [SanitizeHtml]
        public required string NameCrop { get; set; }

        /// <summary>
        /// Gets or sets the address associated with the crop's physical location.
        /// </summary>
        [Required]
        [StringLength(80, MinimumLength = 3, ErrorMessage = "The addres must contain between 3 and 80 characters")]
        [DataType(DataType.Text)]
        [SanitizeHtml]
        public required string AddresCrop { get; set; } 

        /// <summary>
        /// Gets or sets the geographic location (e.g., coordinates, region) of the crop.
        /// </summary>
        [Required]
        [StringLength(124, MinimumLength = 3, ErrorMessage = "The addres must contain between 3 and 124 characters")]
        [DataType(DataType.Text)]
        [SanitizeHtml]
        public required string UbicationCrop { get; set; } 
    }
}