using ArandanoIRT_Backend.Domain.ValueObjects;

namespace ArandanoIRT_Backend.Application.Interfaces.Utilities
{
    public interface ICaptchaService
    {
        /// <summary>
        /// Verifies the CAPTCHA token received from the client.
        /// </summary>
        /// <param name="token">The CAPTCHA response token from the client.</param>
        /// <returns>Result indicating success (verified) or failure.</returns>
        Task<Result> VerifyCaptchaAsync(string token);
    }
}
