namespace ArandanoIRT_Backend.Domain.Entities
{
    public class StatusEntity
    {
        public int IdStatus { get; private set; }
        public string NameStatus { get; private set; }
        public int? TableRelationId { get; private set; }
        public TableRelationEntity? TableRelation { get; private set; } 

        public StatusEntity(int idStatus, string nameStatus, int? tableRelationId = null)
        {
            if (string.IsNullOrWhiteSpace(nameStatus))
                throw new ArgumentException("El nombre del status es requerido.", nameof(nameStatus));

            IdStatus = idStatus;
            NameStatus = nameStatus;
            TableRelationId = tableRelationId;
        }
    }
}