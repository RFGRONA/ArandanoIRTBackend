namespace ArandanoIRT_Backend.Domain.Entities
{
    public class DeviceDataEntity
    {
        public int IdDeviceData { get; private set; }
        public string NameDevice { get; private set; }
        public string DescriptionDevice { get; private set; }
        public short DataCollectionTime { get; private set; }
        public int? StatusId { get; private set; }
        public DateTime RegisteredAt { get; private set; }
        public int? RegisteredBy { get; private set; }
        public DateTime? UpdatedAt { get; private set; }
        public int? UpdatedBy { get; private set; }
        public int? CropId { get; private set; }
        public int? PlantId { get; private set; }

        public DeviceDataEntity(
            int idDeviceData,
            string nameDevice,
            string descriptionDevice,
            short dataCollectionTime,
            DateTime registeredAt,
            int? registeredBy,
            int? statusId,
            int? cropId,
            int? plantId)
        {
            if (dataCollectionTime < 0)
                throw new ArgumentOutOfRangeException(nameof(dataCollectionTime), "El tiempo de recolección no puede ser negativo.");

            IdDeviceData = idDeviceData;
            NameDevice = nameDevice;
            DescriptionDevice = descriptionDevice;
            DataCollectionTime = dataCollectionTime;
            RegisteredAt = registeredAt;
            RegisteredBy = registeredBy;
            StatusId = statusId;
            CropId = cropId;
            PlantId = plantId;
        }
    }
}