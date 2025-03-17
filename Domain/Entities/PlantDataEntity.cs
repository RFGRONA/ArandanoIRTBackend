namespace ArandanoIRT_Backend.Domain.Entities
{
    public class PlantDataEntity
    {
        public int IdPlantState { get; private set; }
        public string NamePlant { get; private set; }
        public DateTime RegisteredAt { get; private set; }
        public DateTime? UpdatedAt { get; private set; }
        public int? StatusId { get; private set; }
        public int? CropId { get; private set; }

        public PlantDataEntity(
            int idPlantState,
            string namePlant,
            DateTime registeredAt,
            int? statusId,
            int? cropId)
        {
            if (string.IsNullOrWhiteSpace(namePlant))
                throw new ArgumentException("El nombre de la planta es requerido.", nameof(namePlant));

            IdPlantState = idPlantState;
            NamePlant = namePlant;
            RegisteredAt = registeredAt;
            StatusId = statusId;
            CropId = cropId;
        }
    }
}