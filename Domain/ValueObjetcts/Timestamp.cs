namespace ArandanoIRT_Backend.Domain.ValueObjetcts
{
    public class Timestamp
    {
        public DateTime Value { get; }

        public Timestamp(DateTime value)
        {
            // Attempting to get the "America/Bogota" time zone.
            // Depending on the system, "SA Pacific Standard Time" may be required on Windows.

            TimeZoneInfo bogotaTimeZone;
            try
            {
                bogotaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");
            }
            catch (TimeZoneNotFoundException)
            {
                bogotaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SA Pacific Standard Time");
            }
            Value = TimeZoneInfo.ConvertTime(value, bogotaTimeZone);
        }

        public static Timestamp Now() => new(DateTime.UtcNow);

        public override string ToString() => Value.ToString("o"); // ISO 8601 format
    }
}