using System.Text.RegularExpressions; 

namespace ArandanoIRT_Backend.Application.Utilities
{
    /// <summary>
    /// Represents a validated, immutable 8-character alphanumeric token.
    /// Provides functionality to generate new random tokens.
    /// </summary>
    public class Token8Utility
    {
        /// <summary>
        /// Gets the validated 8-character alphanumeric string value of the token.
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// Compiled regular expression used to validate the 8-character alphanumeric format (case-insensitive).
        /// </summary>
        private static readonly Regex CodeRegex = new Regex(@"^[A-Za-z0-9]{8}$", RegexOptions.Compiled);

        /// <summary>
        /// Initializes a new instance of the <see cref="Token8Utility"/> class with a pre-validated string value.
        /// </summary>
        /// <param name="value">The 8-character alphanumeric string value.</param>
        /// <exception cref="ArgumentException">Thrown if the provided <paramref name="value"/> is null, whitespace,
        /// or does not match the required 8-character alphanumeric format. The exception message is in Spanish.</exception>
        public Token8Utility(string value)
        {
            // Validates the input value against the regex format.
            if (string.IsNullOrWhiteSpace(value) || !CodeRegex.IsMatch(value))
                // Throws exception with the original Spanish message.
                throw new ArgumentException("El código debe ser alfanumérico y tener 8 caracteres.", nameof(value));
            Value = value;
        }

        /// <summary>
        /// Generates a new, random 8-character alphanumeric token consisting of uppercase letters and digits.
        /// </summary>
        /// <returns>A new <see cref="Token8Utility"/> instance containing the randomly generated token.</returns>
        /// <remarks>
        /// Uses <see cref="System.Random"/> for generation, which is suitable for general purposes but not cryptographically secure.
        /// </remarks>
        public static Token8Utility Generate()
        {
            // Defines the pool of characters allowed in the generated token.
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            // Initializes a pseudo-random number generator. Note: Not cryptographically secure.
            var random = new Random();
            // Creates the character array to hold the generated token.
            var codeChars = new char[8];

            // Populates the array with 8 random characters chosen from the allowed set.
            for (int i = 0; i < 8; i++)
            {
                codeChars[i] = chars[random.Next(chars.Length)];
            }

            // Creates and returns a new instance using the generated character array.
            return new Token8Utility(new string(codeChars));
        }

        /// <summary>
        /// Returns the string representation of the token.
        /// </summary>
        /// <returns>The 8-character alphanumeric string represented by this instance.</returns>
        public override string ToString() => Value;
    }
}