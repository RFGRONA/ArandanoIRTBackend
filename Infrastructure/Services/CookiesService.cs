namespace ArandanoIRT_Backend.Infrastructure.Services
{
    /// <summary>
    /// Provides methods for managing authentication-related HTTP cookies,
    /// such as setting, renewing, retrieving, and removing JWT, Refresh Token,
    /// and StayLoggedIn cookies.
    /// </summary>
    public class CookiesService
    {
        /// <summary>
        /// The domain attribute value to be set on the cookies, read from configuration ("Cookies:Domain").
        /// Can be null if not specified in configuration.
        /// </summary>
        private readonly string? DOMAIN;

        /// <summary>
        /// Initializes a new instance of the <see cref="CookiesService"/> class.
        /// Reads the cookie domain configuration setting.
        /// </summary>
        /// <param name="configuration">The application configuration provider.</param>
        public CookiesService(IConfiguration configuration)
        {
            // Note: The injected configuration is assigned to a local variable `_configuration` which is immediately discarded.
            // The DOMAIN field is assigned directly from the 'configuration' parameter. 
            IConfiguration _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration)); 
            DOMAIN = _configuration["Cookies:Domain"]; // Reads domain, might be null.
        }

        /// <summary>
        /// Sets the authentication cookies (jwt, refreshToken, stayLoggedIn) in the HTTP response.
        /// </summary>
        /// <param name="context">The current <see cref="HttpContext"/>.</param>
        /// <param name="jwtToken">The JWT access token string.</param>
        /// <param name="refreshToken">The refresh token string.</param>
        /// <param name="stayLoggedIn">Flag indicating if the session should be persistent (sets cookie expiry).</param>
        /// <remarks>
        /// Cookies are configured as HttpOnly, Secure, and SameSite=None.
        /// <c>SameSite=None</c> requires <c>Secure=true</c> and implies cross-site usage scenarios.
        /// Expiry is set only if <paramref name="stayLoggedIn"/> is true, otherwise they are session cookies.
        /// </remarks>
        public void SetAuthCookies(HttpContext context, string jwtToken, string refreshToken, bool stayLoggedIn)
        {
            // Defines base options for authentication cookies.
            var baseCookieOptions = new CookieOptions
            {
                HttpOnly = true, // Prevents client-side script access.
                Secure = true,   // Requires HTTPS.
                SameSite = SameSiteMode.None, // Allows cross-site requests (requires Secure=true).
                IsEssential = true, // Marks cookie as essential for application functionality (e.g., GDPR).
                Domain = DOMAIN, // Sets the domain from configuration (can be null).
                Path = "/"       // Sets the cookie path to the root.
            };

            // Creates specific options instances based on the base options.
            var jwtCookieOptions = new CookieOptions(baseCookieOptions);
            var refreshTokenOptions = new CookieOptions(baseCookieOptions);
            var stayLoggedInOption = new CookieOptions(baseCookieOptions);

            // Sets expiration times only if the user opted to stay logged in.
            if (stayLoggedIn)
            {
                // Sets expiry for persistent cookies (e.g., 1 hour for JWT, 30 days for refresh/stayLoggedIn).
                jwtCookieOptions.Expires = DateTime.UtcNow.AddHours(1);
                refreshTokenOptions.Expires = DateTime.UtcNow.AddDays(30);
                stayLoggedInOption.Expires = DateTime.UtcNow.AddDays(30);
            }
            // If stayLoggedIn is false, cookies are session cookies (expire when browser closes).

            // Appends the cookies to the response.
            context.Response.Cookies.Append("jwt", jwtToken, jwtCookieOptions);
            context.Response.Cookies.Append("refreshToken", refreshToken, refreshTokenOptions);
            context.Response.Cookies.Append("stayLoggedIn", stayLoggedIn.ToString(), stayLoggedInOption); // Stores boolean as string.
        }

        /// <summary>
        /// Renews the JWT and Refresh Token cookies using the existing 'stayLoggedIn' preference from the request cookies.
        /// </summary>
        /// <param name="context">The current <see cref="HttpContext"/>.</param>
        /// <param name="jwtToken">The new JWT access token string.</param>
        /// <param name="refreshToken">The new refresh token string.</param>
        public void RenewAuthCookies(HttpContext context, string jwtToken, string refreshToken)
        {
            // Retrieves the current 'stayLoggedIn' setting from the request cookies.
            var stayLoggedIn = GetStayLoggedIn(context);
            // Sets the cookies with the new tokens, maintaining the persistence based on the retrieved flag.
            SetAuthCookies(context, jwtToken, refreshToken, stayLoggedIn);
        }

        /// <summary>
        /// Reads the 'stayLoggedIn' cookie from the request and parses its boolean value.
        /// </summary>
        /// <param name="context">The current <see cref="HttpContext"/>.</param>
        /// <returns><c>true</c> if the 'stayLoggedIn' cookie exists and its value parses to true (case-insensitive); otherwise, <c>false</c>.</returns>
        public bool GetStayLoggedIn(HttpContext context)
        {
            // Attempts to get the cookie value.
            context.Request.Cookies.TryGetValue("stayLoggedIn", out var stayLoggedInValue);
            // Parses the string value to boolean, returning false if parsing fails or cookie doesn't exist.
            return bool.TryParse(stayLoggedInValue, out var result) && result;
        }

        /// <summary>
        /// Reads the 'refreshToken' cookie value from the HTTP request.
        /// </summary>
        /// <param name="context">The current <see cref="HttpContext"/>.</param>
        /// <returns>The refresh token string if the cookie exists; otherwise, <c>null</c>.</returns>
        public string? GetRefreshToken(HttpContext context)
        {
            // Attempts to get the cookie value. TryGetValue returns null if not found.
            context.Request.Cookies.TryGetValue("refreshToken", out var refreshToken);
            return refreshToken;
        }


        /// <summary>
        /// Removes the authentication cookies (jwt, refreshToken, stayLoggedIn) from the HTTP response
        /// by setting their value to empty and their expiration date to the past.
        /// </summary>
        /// <param name="context">The current <see cref="HttpContext"/>.</param>
        public void RemoveCookies(HttpContext context)
        {
            // Defines cookie options with an expiry date in the past to effectively delete them.
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None, // Should match options used when setting.
                Domain = DOMAIN,
                Path = "/",
                Expires = DateTime.UtcNow.AddYears(-1) // Sets expiry to the past.
            };

            // Appends cookies with empty value and past expiry to overwrite existing ones.
            context.Response.Cookies.Append("jwt", string.Empty, cookieOptions);
            context.Response.Cookies.Append("refreshToken", string.Empty, cookieOptions);
            context.Response.Cookies.Append("stayLoggedIn", string.Empty, cookieOptions);

            // Explicitly calls Delete as well for robustness (though Append with past expiry is usually sufficient).
            context.Response.Cookies.Delete("jwt", cookieOptions);
            context.Response.Cookies.Delete("refreshToken", cookieOptions);
            context.Response.Cookies.Delete("stayLoggedIn", cookieOptions);
        }
    }
}