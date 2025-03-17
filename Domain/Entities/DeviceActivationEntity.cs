namespace ArandanoIRT_Backend.Domain.Entities
{
    public class DeviceActivationEntity
    {
        public int IdDeviceActivation { get; private set; }
        public int? DeviceId { get; private set; }
        public string ActivationCode { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime ExpiresAt { get; private set; }
        public DateTime? ActivatedAt { get; private set; }
        public int? ActivationStatus { get; private set; }

        public DeviceActivationEntity(
            int idDeviceActivation,
            int? deviceId,
            string activationCode,
            DateTime createdAt,
            DateTime expiresAt,
            int? activationStatus = null)
        {
            IdDeviceActivation = idDeviceActivation;
            DeviceId = deviceId;
            ActivationCode = activationCode ?? throw new ArgumentNullException(nameof(activationCode));
            CreatedAt = createdAt;
            ExpiresAt = expiresAt;
            ActivationStatus = activationStatus;
        }
    }
}