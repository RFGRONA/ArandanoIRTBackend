namespace ArandanoIRT_Backend.Domain.Entities
{
    public class CropInvitationEntity
    {
        public int IdCropInvitation { get; private set; }
        public string AccesCode { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime ExpiresAt { get; private set; }
        public int? StatusId { get; private set; }
        public int? CreatedBy { get; private set; }
        public int? UsedBy { get; private set; }
        public int CropId { get; private set; }

        public CropInvitationEntity(
            int idCropInvitation,
            string accesCode,
            DateTime createdAt,
            DateTime expiresAt,
            int? statusId,
            int? createdBy,
            int? usedBy,
            int cropId)
        {
            if (string.IsNullOrWhiteSpace(accesCode))
                throw new ArgumentException("El código de acceso es requerido.", nameof(accesCode));

            IdCropInvitation = idCropInvitation;
            AccesCode = accesCode;
            CreatedAt = createdAt;
            ExpiresAt = expiresAt;
            StatusId = statusId;
            CreatedBy = createdBy;
            UsedBy = usedBy;
            CropId = cropId;
        }
    }
}