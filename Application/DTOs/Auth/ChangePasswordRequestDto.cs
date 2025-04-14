namespace ArandanoIRT_Backend.Application.DTOs.Auth
{
    /// <summary>
    /// Represents the request data for changing a user's password using a verification code.
    /// </summary>
    public class ChangePasswordRequestDto
    {
        /// <summary>
        /// Gets or sets the unique code required to authorize the password change.
        /// This code is typically sent to the user via email or another secure channel.
        /// </summary>
        public required string ChangeCode { get; set; }

        /// <summary>
        /// Gets or sets the desired new password for the user account.
        /// </summary>
        public required string NewPassword { get; set; }

        /// <summary>
        /// Gets or sets the confirmation of the new password.
        /// This field is used to ensure the user typed the new password correctly.
        /// </summary>
        public required string ConfirmPassword { get; set; }
    }
}