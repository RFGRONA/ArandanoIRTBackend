using System.ComponentModel.DataAnnotations;

namespace ArandanoIRT_Backend.Application.DTOs.Device
{
    /// <summary>
    /// Represents the complete thermal and image data payload received from a device
    /// via a multipart/form-data request. This DTO groups the data conceptually.
    /// </summary>
    /// <remarks>
    /// In practice, an ASP.NET Core controller endpoint receiving multipart/form-data
    /// would typically bind directly to properties like [FromForm] ThermalDataDto ThermalData
    /// and [FromForm] IFormFile ImageFile, rather than binding to this single DTO directly.
    /// This DTO is more for representing the combined data structure in logic layers.
    /// </remarks>
    public class CaptureDataRequestDto
    {
        /// <summary>
        /// Gets or sets the thermal data and statistics part of the payload (JSON).
        /// </summary>
        [Required] 
        public required ThermalDataDto ThermalData { get; set; }

        /// <summary>
        /// Gets or sets the image file part of the payload (JPEG).
        /// Represented as IFormFile when received via multipart/form-data binding.
        /// </summary>
        [Required]
        public required IFormFile ImageFile { get; set; }
    }
}