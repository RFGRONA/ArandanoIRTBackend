namespace ArandanoIRT_Backend.Application.DTOs.Objetcts // Namespace typo "Objetcts" kept as original
{
    /// <summary>
    /// Represents basic information about a specific crop.
    /// </summary>
    public class CropInfoDto
    {
        /// <summary>
        /// Gets or sets the name assigned to the crop.
        /// </summary>
        public required string NameCrop { get; set; }

        /// <summary>
        /// Gets or sets the address associated with the crop's physical location.
        /// </summary>
        public required string AddresCrop { get; set; } // Property name typo "Addres" kept as original

        /// <summary>
        /// Gets or sets the geographic location (e.g., coordinates, region) of the crop.
        /// </summary>
        public required string UbicationCrop { get; set; } // Property name "UbicationCrop" kept as original
    }
}