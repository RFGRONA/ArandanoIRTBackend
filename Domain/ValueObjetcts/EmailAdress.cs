using System.Text.RegularExpressions;

namespace ArandanoIRT_Backend.Domain.ValueObjetcts
{
    public class EmailAddress
    {
        public string Value { get; }
        private static readonly Regex EmailRegex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

        public EmailAddress(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || !EmailRegex.IsMatch(value))
                throw new ArgumentException("El formato del correo electrónico es inválido.", nameof(value));
            Value = value;
        }

        public override bool Equals(object obj) =>
            obj is EmailAddress other && Value.Equals(other.Value, StringComparison.OrdinalIgnoreCase);

        public override int GetHashCode() => Value.ToLowerInvariant().GetHashCode();

        public override string ToString() => Value;
    }
}