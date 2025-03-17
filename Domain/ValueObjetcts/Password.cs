namespace ArandanoIRT_Backend.Domain.ValueObjetcts
{
    public class Password
    {
        public string Hash { get; }
        private Password(string hash) => Hash = hash;

        // Create and encrypt the password using BCrypt.
        public static Password Create(string plainPassword)
        {
            if (string.IsNullOrWhiteSpace(plainPassword))
                throw new ArgumentException("La contraseña no puede estar vacía.", nameof(plainPassword));

            var hash = BCrypt.Net.BCrypt.HashPassword(plainPassword);
            return new Password(hash);
        }

        // Validates the plaintext password against the hash.
        public bool Validate(string plainPassword) =>
            BCrypt.Net.BCrypt.Verify(plainPassword, Hash);

        public override string ToString() => "[PROTECTED]";
    }
}
