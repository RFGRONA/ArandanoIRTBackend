namespace ArandanoIRT_Backend.Domain.Entities
{
    /// <summary>
    /// Represents a defined status value (e.g., "Active", "Pending", "Used")
    /// that can be applied to various entities within the application.
    /// It can optionally be linked to a specific table type via TableRelation.
    /// </summary>
    public class StatusEntity
    {
        /// <summary>
        /// Gets the unique identifier for the status.
        /// </summary>
        public int IdStatus { get; private set; }

        /// <summary>
        /// Gets the display name or code identifying the status (e.g., "Active", "Pending").
        /// </summary>
        public string NameStatus { get; private set; }

        /// <summary>
        /// Gets the optional foreign key linking this status to a specific table or entity type definition
        /// in the TableRelationEntity, indicating where this status is primarily used. Nullable.
        /// </summary>
        public int? TableRelationId { get; private set; }

        /// <summary>
        /// Gets the optional navigation property to the related <see cref="TableRelationEntity"/>.
        /// This property is typically populated by an ORM (like Entity Framework Core) and may be null otherwise.
        /// </summary>
        public TableRelationEntity? TableRelation { get; private set; } // Assuming TableRelationEntity exists

        /// <summary>
        /// Initializes a new instance of the <see cref="StatusEntity"/> class.
        /// </summary>
        /// <param name="idStatus">The unique ID for the status.</param>
        /// <param name="nameStatus">The name or code identifying the status.</param>
        /// <param name="tableRelationId">The optional ID linking this status to a table/entity type (defaults to null).</param>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="nameStatus"/> is null or whitespace (Spanish message: "El nombre del status es requerido.").
        /// </exception>
        /// <remarks>
        /// The <see cref="TableRelation"/> navigation property is not set by this constructor.
        /// </remarks>
        public StatusEntity(int idStatus, string nameStatus, int? tableRelationId = null)
        {
            // Validates the status name.
            if (string.IsNullOrWhiteSpace(nameStatus))
                throw new ArgumentException("El nombre del status es requerido.", nameof(nameStatus));

            // Assigns validated parameters to the corresponding properties.
            IdStatus = idStatus;
            NameStatus = nameStatus;
            TableRelationId = tableRelationId;
        }
    }
}