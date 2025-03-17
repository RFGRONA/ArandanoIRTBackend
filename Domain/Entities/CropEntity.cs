namespace ArandanoIRT_Backend.Domain.Entities
{
    public class CropEntity
    {
        public int IdCrop { get; private set; }
        public string NameCrop { get; private set; }
        public string AddresCrop { get; private set; }
        public string CityName { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime? UpdatedAt { get; private set; }
        public int? AdminUserId { get; private set; }

        public CropEntity(
            int idCrop,
            string nameCrop,
            string addresCrop,
            string cityName,
            DateTime createdAt,
            int? adminUserId = null)
        {
            if (string.IsNullOrWhiteSpace(nameCrop))
                throw new ArgumentException("El nombre del cultivo es requerido.", nameof(nameCrop));
            if (string.IsNullOrWhiteSpace(addresCrop))
                throw new ArgumentException("La dirección es requerida.", nameof(addresCrop));
            if (string.IsNullOrWhiteSpace(cityName))
                throw new ArgumentException("El nombre de la ciudad es requerido.", nameof(cityName));

            IdCrop = idCrop;
            NameCrop = nameCrop;
            AddresCrop = addresCrop;
            CityName = cityName;
            CreatedAt = createdAt;
            AdminUserId = adminUserId;
        }
    }
}