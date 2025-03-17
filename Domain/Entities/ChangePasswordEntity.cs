using ArandanoIRT_Backend.Domain.ValueObjetcts;

namespace ArandanoIRT_Backend.Domain.Entities
{
    public class ChangePasswordEntity
    {
        public int IdChangePassword { get; private set; }
        public AlphanumericCode8 PasswordResetToken { get; private set; }
        public DateTime ResetTokenExpiresAt { get; private set; }
        public DateTime TokenCreatedAt { get; private set; }
        public int PersonId { get; private set; }

        public ChangePasswordEntity(
            int idChangePassword,
            AlphanumericCode8 passwordResetToken,
            DateTime resetTokenExpiresAt,
            DateTime tokenCreatedAt,
            int personId)
        {
            IdChangePassword = idChangePassword;
            PasswordResetToken = passwordResetToken;
            ResetTokenExpiresAt = resetTokenExpiresAt;
            TokenCreatedAt = tokenCreatedAt;
            PersonId = personId;
        }
    }
}