namespace ArandanoIRT_Backend.Domain.Entities
{
    public class DeviceTokenEntity
    {
        public int IdDeviceToken { get; private set; }
        public int? DeviceId { get; private set; }
        public string Token { get; private set; }
        public string RefreshToken { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime ExpiresAt { get; private set; }
        public DateTime? RevokedAt { get; private set; }
        public string RevokedByIp { get; private set; }
        public string DeviceInfo { get; private set; }
        public string UserAgent { get; private set; }

        public DeviceTokenEntity(
            int idDeviceToken,
            int? deviceId,
            string token,
            string refreshToken,
            DateTime createdAt,
            DateTime expiresAt,
            DateTime? revokedAt,
            string revokedByIp,
            string deviceInfo,
            string userAgent)
        {
            if (string.IsNullOrWhiteSpace(token))
                throw new ArgumentException("El token es requerido.", nameof(token));
            if (string.IsNullOrWhiteSpace(refreshToken))
                throw new ArgumentException("El refresh token es requerido.", nameof(refreshToken));

            IdDeviceToken = idDeviceToken;
            DeviceId = deviceId;
            Token = token;
            RefreshToken = refreshToken;
            CreatedAt = createdAt;
            ExpiresAt = expiresAt;
            RevokedAt = revokedAt;
            RevokedByIp = revokedByIp;
            DeviceInfo = deviceInfo;
            UserAgent = userAgent;
        }
    }
}