namespace ArandanoIRT_Backend.Domain.Entities
{
    public class PlantStateHistoryEntity
    {
        public int IdPlantStateHistory { get; private set; }
        public DateTime ChangedAt { get; private set; }
        public int? ChangedBy { get; private set; }
        public int PlantId { get; private set; }
        public int? StatusId { get; private set; }

        public PlantStateHistoryEntity(
            int idPlantStateHistory,
            DateTime changedAt,
            int plantId,
            int? statusId,
            int? changedBy = null)
        {
            IdPlantStateHistory = idPlantStateHistory;
            ChangedAt = changedAt;
            PlantId = plantId;
            StatusId = statusId;
            ChangedBy = changedBy;
        }
    }
}