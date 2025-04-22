using ArandanoIRT_Backend.Application.DTOs.Auth;
using ArandanoIRT_Backend.Domain.ValueObjects;

namespace ArandanoIRT_Backend.Application.Interfaces.IServices
{
    /// <summary>
    /// Defines the contract for handling help request operations.
    /// </summary>
    public interface IHelpRequestService
    {
        /// <summary>
        /// Processes a help request submitted by an unauthenticated user.
        /// Validates the associated crop name and sends an email notification to the crop's administrator.
        /// </summary>
        /// <param name="request">The help request data transfer object containing user details, message, and crop name.</param>
        /// <returns>A Task representing the asynchronous operation, with a result indicating success or failure.</returns>
        Task<Result> SendUnauthenticatedHelpAsync(HelpRequestDto request);
    }
}