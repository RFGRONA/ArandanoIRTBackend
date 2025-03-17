namespace ArandanoIRT_Backend.Domain.Entities
{
    public class ThermalDataEntity
    {
        public int IdThermalData { get; private set; }
        public string ThermalImageData { get; private set; }
        public byte[] RgbImageData { get; private set; }
        public DateTime RecordedAt { get; private set; }
        public int? PlantId { get; private set; }
        public int? CropId { get; private set; }

        public ThermalDataEntity(
            int idThermalData,
            string thermalImageData,
            byte[] rgbImageData,
            DateTime recordedAt,
            int? plantId,
            int? cropId)
        {
            if (string.IsNullOrWhiteSpace(thermalImageData))
                throw new ArgumentException("Los datos de imagen térmica son requeridos.", nameof(thermalImageData));

            IdThermalData = idThermalData;
            ThermalImageData = thermalImageData;
            RgbImageData = rgbImageData;
            RecordedAt = recordedAt;
            PlantId = plantId;
            CropId = cropId;
        }
    }
}