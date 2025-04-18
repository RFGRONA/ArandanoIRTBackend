using ArandanoIRT_Backend.Application.DTOs.Auth;
using ArandanoIRT_Backend.Application.Utilities;
using ArandanoIRT_Backend.Domain.ValueObjects;
using Serilog;

namespace ArandanoIRT_Backend.Infrastructure.Utilities 
{
    /// <summary>
    /// Implements the <see cref="IEmailService"/> interface using an underlying <see cref="EmailSenderUtility"/>
    /// to send emails via SMTP. Also provides methods to generate standard email body content.
    /// </summary>
    public class SmtpEmailService(IConfiguration configuration, ILogger<SmtpEmailService> logger) : IEmailService 
    {
        /// <summary>
        /// Utility responsible for the actual SMTP sending logic.
        /// </summary>
        private readonly EmailSenderUtility _emailSender = new(configuration);
        /// <summary>
        /// Logger instance for logging email sending events.
        /// </summary>
        private readonly ILogger<SmtpEmailService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        /// <inheritdoc/>
        /// <remarks>
        /// Performs email address validation before attempting to send.
        /// Wraps the potentially synchronous SendEmail method of EmailSenderUtility in Task.Run to conform to the async interface.
        /// Consider making EmailSenderUtility.SendEmail asynchronous using SmtpClient.SendMailAsync for better performance.
        /// </remarks>
        public async Task<Result> SendEmailAsync(string to, string subject, string htmlBody)
        {
            // Validates the recipient email address format and DNS records.
            var validation = await EmailValidatorUtility.ValidateEmailAsync(to);
            if (validation.IsFailure)
            {
                _logger.LogWarning("Attempted to send email to invalid address: {Email}. Error: {Error}", to, validation.ErrorMessage);
                return Result.Failure($"Invalid recipient email address: {to}.");
            }

            try
            {
                await _emailSender.SendEmailAsync(to, subject, htmlBody); 
                return Result.Success();
            }
            catch (Exception ex) // Catches exceptions from Task.Run or SendEmail.
            {
                _logger.LogError(ex, "Failed to send email to {Email} with subject {Subject}", to, subject);
                return Result.Failure($"Failed to send email: {ex.Message}");
            }
        }

        /// <inheritdoc/>
        public string GeneratePasswordResetBody(string userName, string resetLink)
        {
            // Returns the email body content in Spanish as requested.
            return $@"
             <html><body>
             <p>Hola {userName},</p>
             <p>Solicitaste un restablecimiento de contraseña. Por favor, haz clic en el siguiente enlace para restablecer tu contraseña:</p>
             <p><a href='{resetLink}'>Restablecer Contraseña</a></p>
             <p>Si no solicitaste esto, por favor ignora este correo electrónico.</p>
             <p>Este enlace expirará en 30 minutos.</p>
             </body></html>";
        }

        /// <inheritdoc/>
        public string GenerateWelcomeBody(string userName)
        {
            // Returns the email body content in Spanish as requested.
            return $"<html><body><p>¡Bienvenido(a) {userName}!</p><p>Gracias por registrarte.</p></body></html>";
        }

        /// <inheritdoc/>
        public string GenerateAccountConfirmationBody(string userName, string confirmationLink)
        {
            // Returns the email body content in Spanish as requested.
            return $"<html><body><p>Hola {userName},</p><p>Por favor confirma tu dirección de correo electrónico haciendo clic en el siguiente enlace:</p><p><a href='{confirmationLink}'>Confirmar Correo Electrónico</a></p></body></html>";
        }

        /// <inheritdoc/>
        public string GenerateInvitationBody(string inviterName, string cropName, string accessCode, string expiryDate)
        {
            // Returns the email body content in Spanish as requested.
            return $@"
              <html><body>
              <p>Hola,</p>
              <p>{inviterName} te ha invitado a colaborar en el cultivo '{cropName}'.</p>
              <p>Usa el siguiente código de acceso durante el registro:</p>
              <p><strong>{accessCode}</strong></p>
              <p>Este código de invitación expirará el {expiryDate}.</p>
              </body></html>";
        }

        /// <inheritdoc/>
        public string GenerateSuspiciousActivityBody(string userName, DateTime attemptTime, string ipAddress)
        {
            // Returns the email body content in Spanish as requested.
            // Assuming attemptTime is UTC, format it appropriately or convert to local time zone if needed.
            string formattedTime = attemptTime.ToString("yyyy-MM-dd HH:mm:ss 'UTC'"); // Example formatting
            return $@"
             <html><body>
             <p>Hola {userName},</p>
             <p>Detectamos múltiples intentos fallidos de inicio de sesión para tu cuenta alrededor de las {formattedTime} desde la dirección IP: {ipAddress}.</p>
             <p>Si fuiste tú, puedes ignorar este mensaje. Si no reconoces esta actividad, por favor asegura tu cuenta inmediatamente restableciendo tu contraseña o contactando a soporte.</p>
             <p>Gracias,<br/>El Equipo de Arandano IRT</p>
             </body></html>";
        }

        /// <inheritdoc/>
        public string GeneratePasswordResetTokenBody(string userName, string resetToken)
        {
            // Returns the email body content in Spanish as requested. This version generates plain text.
            return $@"
             Hola {userName},
             Solicitaste un restablecimiento de contraseña para tu cuenta de Arandano IRT.
             Tu código de restablecimiento de contraseña es:
             {resetToken}

             Este código expirará en 30 minutos. Si no solicitaste esto, por favor ignora este correo electrónico.

             Gracias,
             El Equipo de Arandano IRT";
        }

        /// <inheritdoc/>
        public string GeneratePasswordChangeConfirmationBody(string userName)
        {
            // Returns the email body content in Spanish as requested.
            return $@"
             <html><body>
             <p>Hola {userName},</p>
             <p>Este correo confirma que la contraseña para tu cuenta de Arandano IRT ha sido cambiada exitosamente.</p>
             <p>Si no realizaste este cambio, por favor contacta a soporte inmediatamente.</p>
             <p>Gracias,<br/>El Equipo de Arandano IRT</p>
             </body></html>";
        }

        /// <inheritdoc/>
        public string GenerateHelpRequestBody(HelpRequestDto request)
        {
            // Validate input request
            if (request == null)
            {
                // Log the error or handle it as appropriate
                _logger.LogWarning("GenerateHelpRequestBody called with a null request object.");
                // Return a generic error message or an empty string, depending on desired behavior
                throw new ArgumentNullException(nameof(request), "Help request cannot be null.");
            }

            // Generate the email body in Spanish
            return $@"
            <html>
            <head>
                <style>
                    body {{ font-family: Arial, sans-serif; line-height: 1.6; }}
                    .container {{ padding: 20px; border: 1px solid #ddd; border-radius: 5px; max-width: 600px; margin: auto; }}
                    h2 {{ color: #333; }}
                    strong {{ color: #555; }}
                </style>
            </head>
            <body>
                <div class='container'>
                    <h2>Nueva Solicitud de Ayuda Recibida</h2>
                    <p>Se ha recibido una nueva solicitud de ayuda con los siguientes detalles:</p>
                    <ul>
                        <li><strong>Nombre:</strong> {System.Net.WebUtility.HtmlEncode(request.Name)}</li>
                        <li><strong>Email:</strong> {System.Net.WebUtility.HtmlEncode(request.Email)}</li>
                        <li><strong>Cultivo Relacionado:</strong> {System.Net.WebUtility.HtmlEncode(request.CropName)}</li>
                        <li><strong>Asunto:</strong> {System.Net.WebUtility.HtmlEncode(request.Subject)}</li>
                    </ul>
                    <hr>
                    <h3>Mensaje:</h3>
                    <p>{System.Net.WebUtility.HtmlEncode(request.Message)}</p>
                    <hr>
                    <p><em>Por favor, contacta al usuario a través del email proporcionado para dar seguimiento.</em></p>
                </div>
            </body>
            </html>";
        }
    }
}