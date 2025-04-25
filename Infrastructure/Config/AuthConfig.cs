using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Security.Claims;
using System.Text;
using Microsoft.Net.Http.Headers;

namespace ArandanoIRT_Backend.Infrastructure.Config
{
    /// <summary>
    /// Provides extension methods for configuring authentication services for the application.
    /// </summary>
    public static class AuthConfig
    {
        /// <summary>
        /// Configures JWT Bearer authentication for the application using settings from the provided configuration.
        /// Reads the JWT secret key, sets up token validation parameters, and configures event handlers
        /// for token extraction and logging.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> to add authentication services to.</param>
        /// <param name="configuration">The application <see cref="IConfiguration"/> instance containing JWT settings under the "Jwt" section.</param>
        /// <returns>The <see cref="IServiceCollection"/> so that additional configuration calls can be chained.</returns>
        /// <exception cref="ArgumentNullException">Thrown if the 'Jwt:Key' configuration value is missing or empty.</exception>
        public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
        {
            // Retrieves the JWT secret key from configuration.
            var jwtKey = configuration["Jwt:Key"];
            if (string.IsNullOrEmpty(jwtKey))
            {
                // Logs a fatal error and throws if the key is missing, as authentication cannot proceed.
                Log.Fatal("JWT Key ('Jwt:Key') is missing in configuration. Authentication cannot be configured.");
                throw new InvalidOperationException("Authentication cannot be configured.");
            }
            // Converts the key to bytes using ASCII encoding.
            var signingKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(jwtKey));

            // Adds authentication services to the service collection.
            services.AddAuthentication(options =>
            {
                // Sets the default authentication schemes to JwtBearer.
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            // Configures the JwtBearer authentication handler.
            .AddJwtBearer(options =>
            {
                // Optional: Saves the validated token in HttpContext.AuthenticateAsync().Result.Properties.
                options.SaveToken = true;
                // Defines the parameters used to validate incoming JWTs.
                options.TokenValidationParameters = CreateTokenValidationParameters(signingKey);
                // Configures event handlers for the JwtBearer authentication process.
                options.Events = CreateJwtBearerEvents();
            });

            // Returns the IServiceCollection for chaining.
            return services;
        }

        /// <summary>
        /// Creates and configures the token validation parameters.
        /// </summary>
        /// <param name="signingKey">The symmetric security key used for signature validation.</param>
        /// <returns>A configured <see cref="TokenValidationParameters"/> object.</returns>
        private static TokenValidationParameters CreateTokenValidationParameters(SymmetricSecurityKey signingKey)
        {
            // var validIssuer = configuration["Jwt:Issuer"];       
            // var validAudience = configuration["Jwt:Audience"];   

            return new TokenValidationParameters
            {
                // --- Primary Validation: Signature Verification ---
                ValidateIssuerSigningKey = true,    // Validates the signature using the provided key.
                IssuerSigningKey = signingKey,      // Sets the key used for signature validation.

                // --- Lifetime Validation ---
                ValidateLifetime = true, // Ensures the token is within its valid time window (not expired, not before nbf).
                ClockSkew = TimeSpan.FromSeconds(30), // Allows a small tolerance for clock differences between servers.

                // --- Issuer & Audience Validation (Enable if Issuer/Audience are set during token creation) ---
                ValidateIssuer = false,             // Set to true to validate the 'iss' claim.
                // ValidIssuer = validIssuer,       // Provide the expected issuer if validating.
                ValidateAudience = false,           // Set to true to validate the 'aud' claim.
                // ValidAudience = validAudience,   // Provide the expected audience if validating.

                // --- Additional Recommended Validations ---
                RequireExpirationTime = true, // Ensures the token has an expiration time ('exp' claim).
                RequireSignedTokens = true, // Ensures the token is signed.
            };
        }

        /// <summary>
        /// Creates and configures the JWT Bearer event handlers.
        /// </summary>
        /// <returns>A configured <see cref="JwtBearerEvents"/> object.</returns>
        private static JwtBearerEvents CreateJwtBearerEvents()
        {
            return new JwtBearerEvents
            {
                // Handles extraction of the token from the incoming request.
                OnMessageReceived = context =>
                {
                    // Defines the order for checking token locations:
                    // 1. Authorization Header (Bearer token) - Preferred standard for APIs.
                    // 2. Cookie ('jwt') - Fallback mechanism, potentially for web UIs.

                    // Checks the Authorization header first.
                    var accessToken = context.Request.Headers[HeaderNames.Authorization].FirstOrDefault();
                    if (!string.IsNullOrEmpty(accessToken) && accessToken.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    {
                        // Extracts the token value from the "Bearer " prefix.
                        context.Token = accessToken.Substring("Bearer ".Length).Trim();
                        Log.Verbose("JWT token received from Authorization header."); // Use Verbose or Debug level for this log.
                    }
                    else
                    {
                        // Falls back to checking the specified cookie if header is missing or invalid.
                        var tokenFromCookie = context.Request.Cookies["jwt"]; // Ensure cookie name matches usage.
                        if (!string.IsNullOrEmpty(tokenFromCookie))
                        {
                            context.Token = tokenFromCookie;
                            Log.Verbose("JWT token received from 'jwt' cookie.");
                        }
                        else
                        {
                            // No token found in either location.
                            Log.Verbose("No JWT token found in Authorization header or 'jwt' cookie.");
                            context.NoResult(); // Explicitly indicates that no token was found in the request.
                        }
                    }
                    return Task.CompletedTask;
                },
                // Handles cases where token validation fails.
                OnAuthenticationFailed = context =>
                {
                    // --- Logging Only ---
                    // Authentication pipeline stops here for failed tokens. Client must handle 401 and potentially refresh.
                    // Logs specific failure reasons for debugging.
                    if (context.Exception is SecurityTokenExpiredException expiredException) // Type pattern matching
                    {
                        Log.Information("JWT authentication failed: Token expired at {Expiry}.", expiredException.Expires);
                        context.Response.Headers.Append("X-Token-Expired", "true");
                    }
                    else if (context.Exception is SecurityTokenInvalidSignatureException)
                    {
                        Log.Warning("JWT authentication failed: Invalid signature.");
                    }
                    else
                    {
                        // Logs other validation exceptions.
                        Log.Warning(context.Exception, "JWT authentication failed: {ExceptionType}", context.Exception.GetType().Name);
                    }
                    // Does not modify the context result; lets the framework handle the failure (typically results in 401).
                    return Task.CompletedTask;
                },
                // Handles successful token validation.
                OnTokenValidated = context =>
                {
                    // This event fires after the token's signature, lifetime, and optionally issuer/audience have been validated.
                    // Additional custom validation logic can be placed here (e.g., check against a revocation list, verify user status).
                    var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                    Log.Information("JWT token validated successfully for User ID: {UserId}", userId ?? "N/A");
                    return Task.CompletedTask;
                },
                // Handles the response when authentication fails and a challenge (401 Unauthorized) is issued.
                OnChallenge = context =>
                {
                    // This event fires when an endpoint requires authentication but it failed or was not provided.
                    // Allows customization of the 401 response if needed.
                    Log.Debug("JWT authentication challenge requested for path: {Path}", context.Request.Path);

                    return Task.CompletedTask;
                }
            };
        }
    }
}