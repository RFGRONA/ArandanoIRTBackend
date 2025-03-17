namespace ArandanoIRT_Backend.Domain.Entities
{
    public class TableRelationEntity
    {
        public int IdTableRelation { get; private set; }
        public string TableName { get; private set; }

        public TableRelationEntity(int idTableRelation, string tableName)
        {
            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentException("El nombre de la tabla es requerido.", nameof(tableName));

            IdTableRelation = idTableRelation;
            TableName = tableName;
        }
    }
}