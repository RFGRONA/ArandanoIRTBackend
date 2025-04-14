namespace ArandanoIRT_Backend.Application.DTOs.Auth
{
    /// <summary>
    /// Represents the request data for initiating the password recovery process.
    /// </summary>
    public class ForgotPasswordRequestDto
    {
        /// <summary>
        /// Gets or sets the email address of the user requesting the password reset.
        /// </summary>
        public required string Email { get; set; }

        /// <summary>
        /// Gets or sets the optional CAPTCHA verification token provided by the user.
        /// This may be required depending on security configurations.
        /// </summary>
        public string? CaptchaToken { get; set; }
    }
}