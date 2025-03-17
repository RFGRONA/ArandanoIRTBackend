namespace ArandanoIRT_Backend.Domain.Entities
{
    public class SensorDataEntity
    {
        public int IdSensorData { get; private set; }
        public double Temperature { get; private set; }         
        public double Humidity { get; private set; }            
        public double LightIntensity { get; private set; }      
        public double? CityTemperature { get; private set; }
        public double? CityHumidity { get; private set; }
        public DateTime RecordedAt { get; private set; }
        public int? PlantId { get; private set; }
        public int? CropId { get; private set; }

        public SensorDataEntity(
            int idSensorData,
            double temperature,
            double humidity,
            double lightIntensity,
            double? cityTemperature,
            double? cityHumidity,
            DateTime recordedAt,
            int? plantId,
            int? cropId)
        {
            if (humidity < 0 || humidity > 100)
                throw new ArgumentOutOfRangeException(nameof(humidity), "La humedad debe estar entre 0 y 100.");
            if (lightIntensity < 0)
                throw new ArgumentOutOfRangeException(nameof(lightIntensity), "La intensidad de la luz no puede ser negativa.");

            IdSensorData = idSensorData;
            Temperature = temperature;
            Humidity = humidity;
            LightIntensity = lightIntensity;
            CityTemperature = cityTemperature;
            CityHumidity = cityHumidity;
            RecordedAt = recordedAt;
            PlantId = plantId;
            CropId = cropId;
        }
    }
}