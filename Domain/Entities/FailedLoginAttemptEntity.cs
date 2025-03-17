namespace ArandanoIRT_Backend.Domain.Entities
{
    public class FailedLoginAttemptEntity
    {
        public int IdFailedLoginAttempt { get; private set; }
        public DateTime AttemptDate { get; private set; }
        public string IpAddress { get; private set; }
        public string DeviceInfo { get; private set; }
        public string UserAgent { get; private set; }
        public int PersonId { get; private set; }

        public FailedLoginAttemptEntity(
            int idFailedLoginAttempt,
            DateTime attemptDate,
            string ipAddress,
            string deviceInfo,
            string userAgent,
            int personId)
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
                throw new ArgumentException("La dirección IP es requerida.", nameof(ipAddress));
            if (string.IsNullOrWhiteSpace(deviceInfo))
                throw new ArgumentException("La información del dispositivo es requerida.", nameof(deviceInfo));
            if (string.IsNullOrWhiteSpace(userAgent))
                throw new ArgumentException("El user agent es requerido.", nameof(userAgent));

            IdFailedLoginAttempt = idFailedLoginAttempt;
            AttemptDate = attemptDate;
            IpAddress = ipAddress;
            DeviceInfo = deviceInfo;
            UserAgent = userAgent;
            PersonId = personId;
        }
    }
}