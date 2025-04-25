using ArandanoIRT_Backend.Application.DTOs.Auth;
using ArandanoIRT_Backend.Domain.ValueObjects;

namespace ArandanoIRT_Backend.Application.Interfaces.IServices
{
    /// <summary>
    /// Defines the contract for user registration operations.
    /// </summary>
    public interface IAuthRegistrationService
    {
        /// <summary>
        /// Registers a new administrator user along with their initial crop.
        /// </summary>
        /// <param name="request">DTO containing admin and crop information.</param>
        /// <returns>A Task representing the asynchronous operation, with a result indicating success or failure.</returns>
        Task<Result> RegisterAdminAsync(RegisterAdminRequestDto request);

        /// <summary>
        /// Registers a new regular user using an invitation code.
        /// </summary>
        /// <param name="request">DTO containing user information and the invitation code.</param>
        /// <returns>A Task representing the asynchronous operation, with a result indicating success or failure.</returns>
        Task<Result> RegisterUserAsync(RegisterUserRequestDto request);
    }
}