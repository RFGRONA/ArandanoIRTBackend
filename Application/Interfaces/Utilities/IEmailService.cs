using ArandanoIRT_Backend.Domain.ValueObjects;

public interface IEmailService
{
    /// <summary>
    /// Sends an email asynchronously to the specified recipient.
    /// </summary>
    /// <param name="to">The email address of the recipient.</param>
    /// <param name="subject">The subject line of the email.</param>
    /// <param name="htmlBody">The body of the email in HTML format.</param>
    /// <returns>A Task representing the asynchronous operation, with a result indicating success or failure.</returns>
    Task<Result> SendEmailAsync(string to, string subject, string htmlBody);

    /// <summary>
    /// Generates the body for a password reset email, including the user's name and reset token.
    /// </summary>
    /// <param name="userName">The name of the user requesting the password reset.</param>
    /// <param name="resetToken">The token used to validate the password reset request.</param>
    /// <returns>HTML email body string for the password reset request.</returns>
    string GeneratePasswordResetBody(string userName, string resetToken);

    /// <summary>
    /// Generates the body for a welcome email, including the user's name.
    /// </summary>
    /// <param name="userName">The name of the new user receiving the welcome message.</param>
    /// <returns>HTML email body string for the welcome email.</returns>
    string GenerateWelcomeBody(string userName);

    /// <summary>
    /// Generates the body for an account confirmation email, including the user's name and confirmation link.
    /// </summary>
    /// <param name="userName">The name of the user confirming their account.</param>
    /// <param name="confirmationLink">The URL link that the user must follow to confirm their account.</param>
    /// <returns>HTML email body string for the account confirmation message.</returns>
    string GenerateAccountConfirmationBody(string userName, string confirmationLink);

    /// <summary>
    /// Generates the body for an invitation email, including the inviter's name, crop name, access code, and expiry date.
    /// </summary>
    /// <param name="inviterName">The name of the person sending the invitation.</param>
    /// <param name="cropName">The name of the crop associated with the invitation.</param>
    /// <param name="accessCode">The access code required to join the invitation.</param>
    /// <param name="expiryDate">The date when the invitation expires.</param>
    /// <returns>HTML email body string for the invitation.</returns>
    string GenerateInvitationBody(string inviterName, string cropName, string accessCode, string expiryDate);

    /// <summary>
    /// Generates the email body for a suspicious login activity warning.
    /// </summary>
    /// <param name="userName">The name of the user.</param>
    /// <param name="attemptTime">The approximate time of the suspicious activity.</param>
    /// <param name="ipAddress">The IP address associated with the attempts.</param>
    /// <returns>HTML email body string for the suspicious activity alert.</returns>
    string GenerateSuspiciousActivityBody(string userName, DateTime attemptTime, string ipAddress);

    /// <summary>
    /// Generates the simple email body containing only the password reset token.
    /// </summary>
    /// <param name="userName">The name of the user.</param>
    /// <param name="resetToken">The password reset token string.</param>
    /// <returns>Plain text or simple HTML email body string.</returns>
    string GeneratePasswordResetTokenBody(string userName, string resetToken);

    /// <summary>
    /// Generates the email body confirming a successful password change.
    /// </summary>
    /// <param name="userName">The name of the user.</param>
    /// <returns>HTML email body string.</returns>
    string GeneratePasswordChangeConfirmationBody(string userName);
}
