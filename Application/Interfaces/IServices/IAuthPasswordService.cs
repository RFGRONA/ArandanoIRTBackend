using ArandanoIRT_Backend.Application.DTOs.Auth;
using ArandanoIRT_Backend.Domain.ValueObjects;

namespace ArandanoIRT_Backend.Application.Interfaces.IServices
{
    /// <summary>
    /// Defines the contract for password management operations like reset requests and execution.
    /// </summary>
    public interface IAuthPasswordService
    {
        /// <summary>
        /// Initiates the password reset process for a given email address.
        /// Generates a reset token, stores it, and typically sends it via email.
        /// Protects against email enumeration attacks.
        /// </summary>
        /// <param name="request">DTO containing the user's email.</param>
        /// <returns>A Task representing the asynchronous operation. Always returns a success result to prevent email enumeration, even if the email doesn't exist or validation fails internally.</returns>
        Task<Result> RequestPasswordResetAsync(ForgotPasswordRequestDto request);

        /// <summary>
        /// Resets the user's password using a valid reset token and a new password.
        /// </summary>
        /// <param name="request">DTO containing the reset token, new password, and confirmation.</param>
        /// <returns>A Task representing the asynchronous operation, with a result indicating success or failure of the password reset.</returns>
        Task<Result> ResetPasswordAsync(ChangePasswordRequestDto request);
    }
}