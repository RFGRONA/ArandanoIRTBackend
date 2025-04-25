using ArandanoIRT_Backend.Application.Interfaces.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArandanoIRT_Backend.UI.Controllers
{
    /// <summary>
    /// Handles cryptographic utility endpoints.
    /// </summary>
    [Route("api/crypto")] 
    [ApiController]
    public class CryptoController : ControllerBase 
    {
        private readonly IRsaService _rsaService;
        private readonly ILogger<CryptoController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="CryptoController"/> class.
        /// </summary>
        /// <param name="rsaService">The service for RSA cryptographic operations.</param>
        /// <param name="logger">The logger for this controller.</param>
        public CryptoController(
            IRsaService rsaService,
            ILogger<CryptoController> logger)
        {
            _rsaService = rsaService ?? throw new ArgumentNullException(nameof(rsaService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Retrieves the server's public RSA key in PEM format.
        /// </summary>
        /// <remarks>
        /// This key is used by clients (e.g., frontend) to encrypt sensitive data like passwords before sending it.
        /// </remarks>
        /// <returns>The public key as a PEM formatted string.</returns>
        /// <response code="200">Public key retrieved successfully. Returns the PEM string.</response>
        /// <response code="500">Internal server error if the key cannot be retrieved.</response>
        [HttpGet("public-key")] 
        [AllowAnonymous] 
        [Produces("text/plain")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
        public IActionResult GetPublicKey()
        {
            _logger.LogInformation("Request received for RSA public key.");
            try
            {
                // Retrieve the public key in PEM format from the service
                string publicKeyPem = _rsaService.GetPublicKeyPem();

                // Check if the key is empty or null 
                if (string.IsNullOrEmpty(publicKeyPem))
                {
                    _logger.LogError("RSA Service returned an empty or null public key.");
                    return StatusCode(StatusCodes.Status500InternalServerError, "Failed to retrieve a valid public key.");
                }

                // Return the key as plain text
                return Ok(publicKeyPem);
            }
            catch (Exception ex)
            {
                // Log any unexpected errors during key retrieval
                _logger.LogError(ex, "An error occurred while retrieving the RSA public key.");
                // Return a generic 500 error
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred while retrieving the public key.");
            }
        }
    }
}