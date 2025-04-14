using ArandanoIRT_Backend.Domain.ValueObjects;

namespace ArandanoIRT_Backend.Application.Interfaces.Utilities
{
    public interface IUserAgentParser
    {
        /// <summary>
        ///  Parses the user agent string and returns a ClientInfo object.
        /// </summary>
        /// <param name="userAgentString"></param>
        /// <returns></returns>
        ClientInfo Parse(string? userAgentString);
    }
}
