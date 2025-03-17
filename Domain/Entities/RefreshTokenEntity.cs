namespace ArandanoIRT_Backend.Domain.Entities
{
    public class RefreshTokenEntity
    {
        public int IdRefreshToken { get; private set; }
        public long Session { get; private set; }
        public string Token { get; private set; }
        public string DeviceInfo { get; private set; }
        public string IpAddress { get; private set; }
        public string UserAgent { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime ExpiresAt { get; private set; }
        public DateTime? RevokedAt { get; private set; }
        public string? RevokedByIp { get; private set; }
        public string? ReplacedByToken { get; private set; }
        public int? PersonId { get; private set; }

        public RefreshTokenEntity(
            int idRefreshToken,
            long session,
            string token,
            string deviceInfo,
            string ipAddress,
            string userAgent,
            DateTime createdAt,
            DateTime expiresAt,
            DateTime? revokedAt,
            string? revokedByIp,
            string? replacedByToken,
            int? personId)
        {
            if (string.IsNullOrWhiteSpace(token))
                throw new ArgumentException("El token es requerido.", nameof(token));
            if (string.IsNullOrWhiteSpace(deviceInfo))
                throw new ArgumentException("La información del dispositivo es requerida.", nameof(deviceInfo));
            if (string.IsNullOrWhiteSpace(ipAddress))
                throw new ArgumentException("La dirección IP es requerida.", nameof(ipAddress));
            if (string.IsNullOrWhiteSpace(userAgent))
                throw new ArgumentException("El user agent es requerido.", nameof(userAgent));

            IdRefreshToken = idRefreshToken;
            Session = session;
            Token = token;
            DeviceInfo = deviceInfo;
            IpAddress = ipAddress;
            UserAgent = userAgent;
            CreatedAt = createdAt;
            ExpiresAt = expiresAt;
            RevokedAt = revokedAt;
            RevokedByIp = revokedByIp;
            ReplacedByToken = replacedByToken;

            PersonId = personId;
        }
    }
}