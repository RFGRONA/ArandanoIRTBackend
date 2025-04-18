namespace ArandanoIRT_Backend.Domain.Entities
{
    /// <summary>
    /// Represents a mapping between a unique identifier and a database table name.
    /// This is likely used to associate other entities (like StatusEntity) with the specific
    /// type of table or entity they relate to.
    /// </summary>
    public class TableRelationEntity
    {
        /// <summary>
        /// Gets the unique identifier for the table relation mapping.
        /// </summary>
        public int IdTableRelation { get; private set; }

        /// <summary>
        /// Gets the name of the database table being referenced.
        /// </summary>
        public string TableName { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TableRelationEntity"/> class.
        /// </summary>
        /// <param name="idTableRelation">The unique ID for this table relation record.</param>
        /// <param name="tableName">The name of the database table.</param>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="tableName"/> is null or whitespace (Spanish message: "El nombre de la tabla es requerido.").
        /// </exception>
        public TableRelationEntity(int idTableRelation, string tableName)
        {
            // Validates that the table name is provided.
            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentException("El nombre de la tabla es requerido.", nameof(tableName));

            // Assigns validated parameters to the corresponding properties.
            IdTableRelation = idTableRelation;
            TableName = tableName;
        }
    }
}