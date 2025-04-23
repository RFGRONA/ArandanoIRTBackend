using Serilog;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;

namespace ArandanoIRT_Backend.Application.Utilities
{
    /// <summary>
    /// Provides utility methods for sending emails using SMTP settings from configuration.
    /// Includes asynchronous methods for sending emails.
    /// </summary>
    public class EmailSenderUtility
    {
        private readonly SmtpClient client;
        private readonly string User;
        private readonly string From;
        private readonly bool EnabledSSL = true; 

        /// <summary>
        /// Initializes a new instance of the <see cref="EmailSenderUtility"/> class,
        /// configuring the SmtpClient from application settings.
        /// </summary>
        /// <param name="configuration">The application configuration containing SMTP settings.</param>
        /// <exception cref="ArgumentNullException">Thrown if configuration is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown if required SMTP configuration keys are missing or the Port value is not a valid integer format or range.</exception>
        public EmailSenderUtility(IConfiguration configuration)
        {
            // Ensure configuration itself is not null
            ArgumentNullException.ThrowIfNull(configuration);

            // Reads SMTP configuration values. Use InvalidOperationException for missing config.
            string Host = configuration["Smtp:Host"] ?? throw new InvalidOperationException("Required configuration 'Smtp:Host' is missing.");
            string? portString = configuration["Smtp:Port"]; // Get value, could be null
            User = configuration["Smtp:User"] ?? throw new InvalidOperationException("Required configuration 'Smtp:User' is missing.");
            From = configuration["Smtp:From"] ?? throw new InvalidOperationException("Required configuration 'Smtp:From' is missing.");
            string Password = configuration["Smtp:Password"] ?? throw new InvalidOperationException("Required configuration 'Smtp:Password' is missing.");

            // Check if Port configuration exists
            if (string.IsNullOrWhiteSpace(portString))
            {
                throw new InvalidOperationException("Required configuration 'Smtp:Port' is missing or empty.");
            }

            // Use int.TryParse for safe parsing
            if (!int.TryParse(portString, out int portValue))
            {
                // Throw exception if TryParse fails (invalid format)
                throw new InvalidOperationException($"Configuration value for 'Smtp:Port' ('{portString}') is not a valid integer format.");
            }

            // Optional: Validate port range
            if (portValue <= 0 || portValue > 65535)
            {
                throw new InvalidOperationException($"Configuration value for 'Smtp:Port' ('{portValue}') is outside the valid port range (1-65535).");
            }

            // Initializes the SmtpClient instance.
            client = new SmtpClient(Host, portValue)
            {
                EnableSsl = EnabledSSL, 
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(User, Password)
            };

            Log.Information("EmailSenderUtility initialized for host {SmtpHost}:{SmtpPort}", Host, portValue);
        }

        /// <summary>
        /// Asynchronously sends a simple email message with HTML body content.
        /// </summary>
        /// <param name="destiny">The recipient's email address.</param>
        /// <param name="affair">The subject line of the email.</param>
        /// <param name="message">The HTML body content of the email.</param>
        /// <returns>A Task representing the asynchronous operation.</returns>
        /// <remarks>
        /// This method sends the email asynchronously using SendMailAsync for better performance and non-blocking I/O.
        /// Exceptions during sending are caught and logged using Serilog, but not propagated.
        /// The sender display name is currently hardcoded as "Biokudi". Consider making it configurable.
        /// This method signature is a BREAKING CHANGE from any previous synchronous version. Callers must now use 'await'.
        /// </remarks>
        public async Task SendEmailAsync(string destiny, string affair, string message)
        {
            try
            {
                // Basic argument validation
                ArgumentException.ThrowIfNullOrWhiteSpace(destiny);
                ArgumentException.ThrowIfNullOrWhiteSpace(affair);
                ArgumentException.ThrowIfNullOrWhiteSpace(message);

                // Configures the email message object using 'using' for disposable MailMessage
                using MailMessage email = new()
                {
                    From = new MailAddress(From, "Biokudi"), 
                    Subject = affair,
                    Body = message,
                    IsBodyHtml = true
                };
                email.To.Add(destiny);

                // <<< Replaced Send with await SendMailAsync >>>
                await client.SendMailAsync(email);

                // Log success after await completes
                Log.Information("Successfully sent email asynchronously. To: {Recipient}, Subject: {Subject}", destiny, affair);
            }
            catch (ArgumentException argEx) // Catch specific validation errors
            {
                Log.Warning(argEx, "Invalid argument provided for SendEmailAsync. To: {Recipient}", destiny);
                // Decide if this should re-throw or just be logged. Currently logged only.
            }
            catch (SmtpException smtpEx) // Catch specific SMTP errors
            {
                Log.Error(smtpEx, "SMTP error sending email asynchronously. To: {Recipient}, Subject: {Subject}. Status Code: {StatusCode}", destiny, affair, smtpEx.StatusCode);
            }
            catch (Exception ex)
            {
                // Logs any other error that occurs during email preparation or sending.
                Log.Error(ex, "ERROR SENDING EMAIL ASYNC. To: {Recipient}, Subject: {Subject}", destiny, affair);
            }
        }

        /// <summary>
        /// Asynchronously sends an email message with an HTML body and a single PDF attachment provided as a Base64 string.
        /// </summary>
        /// <param name="recipient">The recipient's email address.</param>
        /// <param name="subject">The subject line of the email.</param>
        /// <param name="message">The HTML body content of the email.</param>
        /// <param name="fileBase64">The Base64 encoded string representation of the attachment file content.</param>
        /// <param name="fileName">The desired name for the attached file (e.g., "report.pdf"). Should include extension.</param>
        /// <returns>A Task representing the asynchronous operation.</returns>
        /// <remarks>
        /// This method sends the email asynchronously using SendMailAsync.
        /// It includes an asynchronous validation check on the recipient's email format.
        /// It assumes the provided Base64 string represents a PDF file (<c>MediaTypeNames.Application.Pdf</c>). Consider making content type dynamic or a parameter.
        /// Exceptions during sending are caught and logged using Serilog, but not propagated.
        /// This method signature is a BREAKING CHANGE from any previous synchronous version. Callers must now use 'await'.
        /// </remarks>
        public async Task SendEmailWithAttachmentAsync(string recipient, string subject, string message, string fileBase64, string fileName)
        {
            try
            {
                // Basic argument validation
                ArgumentException.ThrowIfNullOrWhiteSpace(recipient);
                ArgumentException.ThrowIfNullOrWhiteSpace(subject);
                ArgumentException.ThrowIfNullOrWhiteSpace(message);
                ArgumentException.ThrowIfNullOrWhiteSpace(fileBase64);
                ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

                // Validate email asynchronously
                var validationResult = await EmailValidatorUtility.ValidateEmailAsync(recipient);
                if (!validationResult.IsSuccess)
                {
                    Log.Warning("Invalid recipient email format detected asynchronously: {Recipient}. Error: {Error}", recipient, validationResult.ErrorMessage);
                    // Throwing FormatException to maintain original behavior on validation failure
                    throw new FormatException($"Invalid recipient email format: {recipient}");
                }

                // Decode Base64 (consider TryParse if base64 source is unreliable)
                byte[] fileBytes;
                try
                {
                    fileBytes = Convert.FromBase64String(fileBase64);
                }
                catch (FormatException formatEx)
                {
                    Log.Error(formatEx, "Invalid Base64 string for attachment. Recipient: {Recipient}", recipient);
                    throw new ArgumentException("Invalid Base64 format for attachment.", nameof(fileBase64), formatEx);
                }

                // Create MailMessage and Attachment using 'using' statements
                using MailMessage email = new(From, recipient, subject, message) { IsBodyHtml = true };
                using MemoryStream ms = new(fileBytes);
                // Consider making content type a parameter if attachments aren't always PDF
                using Attachment attachment = new(ms, fileName, MediaTypeNames.Application.Pdf); // Attachment is IDisposable
                email.Attachments.Add(attachment);

                // <<< Replaced Send with await SendMailAsync >>>
                await client.SendMailAsync(email);

                // Log success after await completes
                Log.Information("Successfully sent email with attachment asynchronously. To: {Recipient}, Subject: {Subject}, Attachment: {FileName}", recipient, subject, fileName);
            }
            catch (ArgumentException argEx) 
            {
                Log.Warning(argEx, "Invalid argument provided for SendEmailWithAttachmentAsync. Recipient: {Recipient}", recipient);
            }
            catch (FormatException formatEx) 
            {
                Log.Warning(formatEx, "Format error during SendEmailWithAttachmentAsync. Recipient: {Recipient}", recipient);
            }
            catch (SmtpException smtpEx) 
            {
                Log.Error(smtpEx, "SMTP error sending email with attachment asynchronously. To: {Recipient}, Subject: {Subject}. Status Code: {StatusCode}", recipient, subject, smtpEx.StatusCode);
            }
            catch (Exception ex)
            {
                // Logs any other error that occurs during email preparation, attachment processing, or sending.
                Log.Error(ex, "ERROR SENDING EMAIL WITH ATTACHMENT ASYNC. To: {Recipient}, Subject: {Subject}", recipient, subject);
            }
        }
    } 
} 