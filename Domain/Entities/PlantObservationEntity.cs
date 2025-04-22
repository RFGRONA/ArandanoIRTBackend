namespace ArandanoIRT_Backend.Domain.Entities
{
    /// <summary>
    /// Represents a single observation record made about a specific plant,
    /// detailing visual characteristics, subjective ratings, notes, and potentially linked sensor data identifiers.
    /// </summary>
    /// <remarks>
    /// Properties in this entity use public getters and setters, allowing modification after instantiation.
    /// </remarks>
    public class PlantObservationEntity
    {
        /// <summary>
        /// Gets or sets the unique identifier for this plant observation record.
        /// </summary>
        public int IdObservation { get; set; }

        /// <summary>
        /// Gets or sets the date and time (usually UTC) when the observation was created or recorded.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets a general description of the observation.
        /// </summary>
        /// <remarks>
        /// Initialized with null-forgiving operator; expected to be non-null after construction due to validation.
        /// </remarks>
        public string Description { get; set; } = null!;

        /// <summary>
        /// Gets or sets a value indicating whether discolorations were observed on the plant.
        /// </summary>
        public bool HasDecolorations { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether altered uniformity (e.g., in size or shape) was observed.
        /// </summary>
        public bool HasAlteredUniformity { get; set; }

        /// <summary>
        /// Gets or sets specific notes related to the plant's leaves or stems. Nullable.
        /// </summary>
        public string? LeafStemNotes { get; set; }

        /// <summary>
        /// Gets or sets a subjective rating provided during the observation, expected to be between 1 and 3.
        /// </summary>
        public short SubjectiveRating { get; set; }

        /// <summary>
        /// Gets or sets any additional free-text notes related to the observation. Nullable.
        /// </summary>
        public string? AdditionalNotes { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier of the user who created this observation record. Nullable.
        /// </summary>
        public int? CreatedBy { get; set; }

        /// <summary>
        /// Gets or sets the identifier representing the status assigned to this observation or the plant at the time of observation. Nullable.
        /// </summary>
        public int? StatusId { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier of the plant to which this observation pertains. Nullable.
        /// </summary>
        public int? PlantId { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the last relevant general sensor data record associated with this observation. Nullable.
        /// </summary>
        public int? LastSensorDataId { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the last relevant thermal sensor data record associated with this observation. Nullable.
        /// </summary>
        public int? LastThermalDataId { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PlantObservationEntity"/> class.
        /// </summary>
        /// <param name="idObservation">The unique ID for this observation record.</param>
        /// <param name="createdAt">The creation timestamp.</param>
        /// <param name="description">A general description of the observation.</param>
        /// <param name="hasDecolorations">Indicates if discolorations were observed.</param>
        /// <param name="hasAlteredUniformity">Indicates if altered uniformity was observed.</param>
        /// <param name="subjectiveRating">A subjective rating (must be between 1 and 3).</param>
        /// <param name="createdBy">The ID of the user creating the observation (optional).</param>
        /// <param name="statusId">The status ID associated with the observation (optional).</param>
        /// <param name="plantId">The ID of the observed plant (optional).</param>
        /// <param name="lastSensorDataId">ID of the last relevant sensor data record (optional).</param>
        /// <param name="lastThermalDataId">ID of the last relevant thermal data record (optional).</param>
        /// <param name="leafStemNotes">Specific notes about leaves/stems (optional).</param>
        /// <param name="additionalNotes">Additional free-text notes (optional).</param>
        /// <exception cref="ArgumentException">Thrown if <paramref name="description"/> is null or empty
        /// (Spanish message: "La descripción no puede ser nula o vacía.").</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="subjectiveRating"/> is not between 1 and 3
        /// (Spanish message: "La calificación subjetiva debe estar entre 1 y 3.").</exception>
        public PlantObservationEntity(
            int idObservation,
            DateTime createdAt,
            string description,
            bool hasDecolorations,
            bool hasAlteredUniformity,
            short subjectiveRating,
            int? createdBy = null,
            int? statusId = null,
            int? plantId = null,
            int? lastSensorDataId = null,
            int? lastThermalDataId = null,
            string? leafStemNotes = null,
            string? additionalNotes = null)
        {

            if (string.IsNullOrEmpty(description))
            {
                throw new ArgumentException("The description cannot be null or empty.", nameof(description));
            }
            if (subjectiveRating < 1 || subjectiveRating > 3)
            {
                throw new ArgumentOutOfRangeException(nameof(subjectiveRating), "The subjective rating must be between 1 and 3.");
            }

            // Assigns parameters to the corresponding properties.
            IdObservation = idObservation;
            CreatedAt = createdAt;
            Description = description;
            HasDecolorations = hasDecolorations;
            HasAlteredUniformity = hasAlteredUniformity;
            SubjectiveRating = subjectiveRating;
            CreatedBy = createdBy;
            StatusId = statusId;
            PlantId = plantId;
            LastSensorDataId = lastSensorDataId;
            LastThermalDataId = lastThermalDataId;
            LeafStemNotes = leafStemNotes;
            AdditionalNotes = additionalNotes;
        }
    }
}