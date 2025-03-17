namespace ArandanoIRT_Backend.Domain.Entities
{
    public class AuditPersonEntity
    {
        public int IdAuditPerson { get; private set; }
        public string ColumnName { get; private set; }
        public int RecordId { get; private set; }
        public int CropId { get; private set; }
        public string Action { get; private set; }
        public DateTime? PerformedAt { get; private set; }
        public int? PerformedBy { get; private set; }
        public string? PerformedByIp { get; private set; }
        public string? UserAgent { get; private set; }

        public AuditPersonEntity(
            int idAuditPerson,
            string columnName,
            int recordId,
            int cropId,
            string action,
            DateTime? performedAt,
            int? performedBy,
            string? performedByIp,
            string? userAgent)
        {
            if (string.IsNullOrWhiteSpace(columnName))
                throw new ArgumentException("El nombre de la columna es requerido.", nameof(columnName));
            if (string.IsNullOrWhiteSpace(action))
                throw new ArgumentException("La acción es requerida.", nameof(action));

            IdAuditPerson = idAuditPerson;
            ColumnName = columnName;
            RecordId = recordId;
            CropId = cropId;
            Action = action;
            PerformedAt = performedAt;
            PerformedBy = performedBy;
            PerformedByIp = performedByIp;
            UserAgent = userAgent;
        }
    }
}