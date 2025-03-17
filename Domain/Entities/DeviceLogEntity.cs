namespace ArandanoIRT_Backend.Domain.Entities
{
    public class DeviceLogEntity
    {
        public int IdDeviceLog { get; private set; }
        public int DeviceId { get; private set; }
        public string LogType { get; private set; }
        public string LogMessage { get; private set; }
        public DateTime LogTimestamp { get; private set; }

        public DeviceLogEntity(
            int idDeviceLog,
            int deviceId,
            string logType,
            string logMessage,
            DateTime logTimestamp)
        {
            if (string.IsNullOrWhiteSpace(logType))
                throw new ArgumentException("El tipo de log es requerido.", nameof(logType));
            if (string.IsNullOrWhiteSpace(logMessage))
                throw new ArgumentException("El mensaje de log es requerido.", nameof(logMessage));

            IdDeviceLog = idDeviceLog;
            DeviceId = deviceId;
            LogType = logType;
            LogMessage = logMessage;
            LogTimestamp = logTimestamp;
        }
    }
}