using Serilog;
using System.Net;
using System.Net.Mail;
using System.Net.Mime; 

namespace ArandanoIRT_Backend.Application.Utilities
{
    /// <summary>
    /// Provides utility methods for sending emails using SMTP settings from configuration.
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
        /// <exception cref="ArgumentNullException">Thrown if required SMTP configuration keys (Host, Port, User, From, Password) are missing.</exception>
        /// <exception cref="FormatException">Thrown if the SMTP Port configuration value is not a valid integer.</exception>
        public EmailSenderUtility(IConfiguration configuration)
        {

            // Reads SMTP configuration values, throwing if any are missing.
            string Host = configuration["Smtp:Host"] ?? throw new ArgumentNullException(nameof(configuration), "Smtp:Host configuration is missing.");
            int Port = int.Parse(configuration["Smtp:Port"] ?? throw new ArgumentNullException(nameof(configuration), "Smtp:Port configuration is missing."));
            User = configuration["Smtp:User"] ?? throw new ArgumentNullException(nameof(configuration), "Smtp:User configuration is missing.");
            From = configuration["Smtp:From"] ?? throw new ArgumentNullException(nameof(configuration), "Smtp:From configuration is missing.");
            string Password = configuration["Smtp:Password"] ?? throw new ArgumentNullException(nameof(configuration), "Smtp:Password configuration is missing.");

            // Initializes the SmtpClient instance.
            client = new SmtpClient(Host, Port)
            {
                EnableSsl = EnabledSSL, 
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(User, Password) 
            };
        }

        /// <summary>
        /// Sends a simple email message with HTML body content.
        /// </summary>
        /// <param name="destiny">The recipient's email address.</param> // Original param name kept
        /// <param name="affair">The subject line of the email.</param> // Original param name kept
        /// <param name="message">The HTML body content of the email.</param>
        /// <remarks>
        /// This method sends the email synchronously. Exceptions during sending are caught and logged using Serilog, but not propagated.
        /// The sender display name is hardcoded as "Biokudi".
        /// </remarks>
        public void SendEmail(string destiny, string affair, string message)
        {
            MailMessage email;
            try
            {
                // Configures the email message object.
                email = new MailMessage
                {
                    From = new MailAddress(From, "Biokudi"), 
                    Subject = affair,
                    Body = message,
                    IsBodyHtml = true 
                };
                // Adds the recipient address.
                email.To.Add(destiny);

                // Attempts to send the email synchronously.
                client.Send(email);
            }
            catch (Exception ex)
            {
                // Logs any error that occurs during email preparation or sending.
                Log.Error("ERROR SENDING EMAIL: ", ex);
            }
        }

        /// <summary>
        /// Sends an email message with an HTML body and a single PDF attachment provided as a Base64 string.
        /// </summary>
        /// <param name="recipient">The recipient's email address.</param>
        /// <param name="subject">The subject line of the email.</param>
        /// <param name="message">The HTML body content of the email.</param>
        /// <param name="fileBase64">The Base64 encoded string representation of the attachment file content.</param>
        /// <param name="fileName">The desired name for the attached file (e.g., "report.pdf").</param>
        /// <remarks>
        /// This method sends the email synchronously. It includes a synchronous validation check on the recipient's email format.
        /// It assumes the provided Base64 string represents a PDF file (<c>MediaTypeNames.Application.Pdf</c>).
        /// Exceptions during sending are caught and logged using Serilog, but not propagated.
        /// </remarks>
        public void SendEmailWithAttachment(string recipient, string subject, string message, string fileBase64, string fileName)
        {
            try
            {
                // Validates recipient email format synchronously. Blocks until validation completes.
                var validationResult = EmailValidatorUtility.ValidateEmailAsync(recipient).Result;
                if (!validationResult.IsSuccess)
                    throw new FormatException($"Invalid recipient email format: {recipient}"); 

                // Decodes the Base64 attachment content into bytes.
                byte[] fileBytes = Convert.FromBase64String(fileBase64);

                // Creates the mail message using a 'using' statement for disposable resources.
                using (MailMessage email = new(From, recipient, subject, message)) // Use configured 'From' address.
                {
                    email.IsBodyHtml = true; // Assumes message content is HTML.

                    // Creates a memory stream from the decoded file bytes using 'using'.
                    using (MemoryStream ms = new(fileBytes))
                    {
                        // Creates the attachment, specifying PDF content type.
                        Attachment attachment = new(ms, fileName, MediaTypeNames.Application.Pdf);
                        // Adds the attachment to the email.
                        email.Attachments.Add(attachment);

                        // Attempts to send the email synchronously.
                        client.Send(email);
                    } // MemoryStream is disposed here.
                } // MailMessage is disposed here.
            }
            catch (Exception ex)
            {
                // Logs any error that occurs during email preparation, attachment processing, or sending.
                Log.Error("ERROR SENDING EMAIL WITH ATTACHMENT: ", ex);
            }
        }
    }
}