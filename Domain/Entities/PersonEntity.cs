using ArandanoIRT_Backend.Domain.ValueObjetcts;

namespace ArandanoIRT_Backend.Domain.Entities
{
    public class PersonEntity
    {
        public int IdPerson { get; private set; }
        public string FirstName { get; private set; }
        public string LastName { get; private set; }
        public EmailAddress Email { get; private set; }
        public Password Password { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime? UpdatedAt { get; private set; }
        public DateTime? LastLoginAt { get; private set; }
        public DateTime? LastPasswordChangeAt { get; private set; }
        public bool IsAdmin { get; private set; }
        public bool AllNotifications { get; private set; }
        public int? CropId { get; private set; }

        public PersonEntity(
            int idPerson,
            string firstName,
            string lastName,
            EmailAddress email,
            Password password,
            DateTime createdAt,
            bool isAdmin,
            bool allNotifications,
            int? cropId = null)
        {
            if (string.IsNullOrWhiteSpace(firstName))
                throw new ArgumentException("El nombre es requerido.", nameof(firstName));
            if (string.IsNullOrWhiteSpace(lastName))
                throw new ArgumentException("El apellido es requerido.", nameof(lastName));

            IdPerson = idPerson;
            FirstName = firstName;
            LastName = lastName;
            Email = email;
            Password = password;
            CreatedAt = createdAt;
            IsAdmin = isAdmin;
            AllNotifications = allNotifications;
            CropId = cropId;
        }

        public void Authenticate(string plainPassword)
        {
            if (!Password.Validate(plainPassword))
                throw new UnauthorizedAccessException("Contraseña inválida.");
        }
    }
}