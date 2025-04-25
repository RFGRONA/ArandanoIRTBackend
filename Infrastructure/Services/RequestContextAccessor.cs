using ArandanoIRT_Backend.Application.Interfaces.Utilities; 
using ArandanoIRT_Backend.Domain.ValueObjects;
using Microsoft.Extensions.Primitives;
using System.Security.Claims;

namespace ArandanoIRT_Backend.Infrastructure.Services
{
    /// <summary>
    /// Implements <see cref="IRequestContextAccessor"/> to provide access to common data
    /// from the current HTTP request context, such as IP address, User Agent details,
    /// and authenticated user information (ID, Crop ID). Designed to be used within a request scope.
    /// </summary>
    public class RequestContextAccessor : IRequestContextAccessor
    {
        /// <summary>
        /// Accessor for the current HttpContext, provided by ASP.NET Core.
        /// </summary>
        private readonly IHttpContextAccessor _httpContextAccessor;
        /// <summary>
        /// Service used to parse User-Agent strings into structured ClientInfo objects.
        /// </summary>
        private readonly IUserAgentParser _userAgentParser;
        /// <summary>
        /// Cached ClientInfo for the current request scope to avoid repeated parsing. Null initially.
        /// </summary>
        private ClientInfo? _cachedClientInfo;

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestContextAccessor"/> class.
        /// </summary>
        /// <param name="httpContextAccessor">The accessor for the current HTTP context.</param>
        /// <param name="userAgentParser">The service for parsing User-Agent strings.</param>
        /// <exception cref="ArgumentNullException">Thrown if httpContextAccessor or userAgentParser is null.</exception>
        public RequestContextAccessor(IHttpContextAccessor httpContextAccessor, IUserAgentParser userAgentParser)
        {
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
            _userAgentParser = userAgentParser ?? throw new ArgumentNullException(nameof(userAgentParser));
        }

        /// <summary>
        /// Gets the current <see cref="HttpContext"/> associated with the request scope.
        /// </summary>
        /// <returns>The current <see cref="HttpContext"/>, or <c>null</c> if accessed outside a request context.</returns>
        private HttpContext? CurrentHttpContext => _httpContextAccessor.HttpContext;

        /// <inheritdoc/>
        public string GetIpAddress()
        {
            // Returns "Unknown" if HttpContext is not available.
            if (CurrentHttpContext == null) return "Unknown";

            // Tries to get the IP address from the X-Forwarded-For header first,
            // which is common when behind proxies or load balancers. Takes the first IP in the list.
            if (CurrentHttpContext.Request.Headers.TryGetValue("X-Forwarded-For", out StringValues forwardedFor))
            {
                // Handles potential comma-separated list in the header.
                return forwardedFor.FirstOrDefault()?.Split(',').Select(s => s.Trim()).FirstOrDefault() ?? "Unknown";
            }
            // Falls back to the direct connection's remote IP address if the header is not present.
            return CurrentHttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        }

        /// <inheritdoc/>
        public string GetUserAgent()
        {
            // Returns "Unknown" if HttpContext is not available.
            var userAgentValues = CurrentHttpContext?.Request.Headers.UserAgent ?? StringValues.Empty;

            // Checks if the User-Agent header is empty or null.
            if (StringValues.IsNullOrEmpty(userAgentValues))
            {
                return "Unknown"; 
            }
            // If the User-Agent header is present, return its value as a string.
            return userAgentValues.ToString();
        }

        /// <inheritdoc/>
        public ClientInfo GetClientInfo()
        {
            // Caches the parsed ClientInfo within the request scope to avoid redundant parsing.
            // If the cached value is null (first call in the scope), parse the User-Agent string.
            if (_cachedClientInfo == null)
            {
                // Parses the current User-Agent string using the injected parser service.
                _cachedClientInfo = _userAgentParser.Parse(GetUserAgent());
            }
            // Returns the cached or newly parsed ClientInfo object.
            return _cachedClientInfo;
        }

        /// <inheritdoc/>
        public string GetFormattedDeviceInfo()
        {
            // Retrieves the structured ClientInfo and calls its formatting method.
            return GetClientInfo().ToFormattedString();
        }

        /// <inheritdoc/>
        public int? GetCurrentUserId()
        {
            // Attempts to find the NameIdentifier claim (standard claim type for user ID)
            // within the current authenticated user's principal.
            var userIdClaim = CurrentHttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            // Parses the claim value to an integer, returning null if the claim is missing or parsing fails.
            return int.TryParse(userIdClaim, out int userId) ? userId : null;
        }

        /// <inheritdoc/>
        public int? GetCurrentCropId()
        {
            // Attempts to find a custom claim named "CropId".
            // Ensure the claim name here matches the name used when generating the JWT.
            var cropIdClaim = CurrentHttpContext?.User?.FindFirstValue("CropId");
            // Parses the claim value to an integer, returning null if the claim is missing or parsing fails.
            return int.TryParse(cropIdClaim, out int cropId) ? cropId : null;
        }
    }
}