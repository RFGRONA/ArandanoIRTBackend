using System.Text.RegularExpressions;

namespace ArandanoIRT_Backend.Domain.ValueObjetcts
{
    public class AlphanumericCode8
    {
        public string Value { get; }

        private static readonly Regex CodeRegex = new Regex(@"^[A-Za-z0-9]{8}$", RegexOptions.Compiled);

        public AlphanumericCode8(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || !CodeRegex.IsMatch(value))
                throw new ArgumentException("El código debe ser alfanumérico y tener 8 caracteres.", nameof(value));
            Value = value;
        }
        public static AlphanumericCode8 Generate()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            var codeChars = new char[8];

            for (int i = 0; i < 8; i++)
            {
                codeChars[i] = chars[random.Next(chars.Length)];
            }

            return new AlphanumericCode8(new string(codeChars));
        }

        public override string ToString() => Value;
    }
}
