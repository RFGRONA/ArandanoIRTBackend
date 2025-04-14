using System.Globalization; 
using System.Text.RegularExpressions; 
using DnsClient; 
using ArandanoIRT_Backend.Domain.ValueObjects;

namespace ArandanoIRT_Backend.Application.Utilities
{
    /// <summary>
    /// Provides static utility methods for validating email addresses,
    /// including syntax checks and DNS record verification (MX/A).
    /// </summary>
    public static class EmailValidatorUtility
    {
        /// <summary>
        /// Compiled regular expression for validating the basic syntax of an email address.
        /// </summary>
        /// <remarks>
        /// This regex aims for a balance between RFC compliance and practical use cases.
        /// </remarks>
        private static readonly Regex EmailRegex = new Regex(
            @"^(?("")(""[^""]+?""@)|(([0-9a-zA-Z]((\.|[-!#\$%&'\*\+/=\?\^`\{\}\|~\w])*[0-9a-zA-Z])?)@))" +
            @"(?(\[)(\[(\d{1,3}\.){3}\d{1,3}\])|(([0-9a-zA-Z][-\w]*[0-9a-zA-Z]*\.)+[a-zA-Z]{2,}))$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Asynchronously validates an email address for correct syntax and checks for valid DNS (MX or A) records for its domain.
        /// </summary>
        /// <param name="email">The email address to validate.</param>
        /// <returns>
        /// A <see cref="Task{TResult}"/> representing the asynchronous operation, containing a
        /// <see cref="Result"/> object. The result indicates success if the email is valid,
        /// or failure with an error message if validation fails at any step (syntax or DNS).
        /// </returns>
        public static async Task<Result> ValidateEmailAsync(string email)
        {
            // 1. Validates email syntax first.
            var syntaxResult = IsValidEmailSyntax(email);
            if (syntaxResult.IsFailure)
                return syntaxResult;

            // 2. Validates DNS records (MX/A) for the domain.
            return await HasValidMxRecordAsync(email);
        }

        /// <summary>
        /// Validates the basic syntax of an email address using a regular expression.
        /// </summary>
        /// <param name="email">The email address string to check.</param>
        /// <returns>A <see cref="Result"/> indicating success or failure for syntax validation.</returns>
        private static Result IsValidEmailSyntax(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return Result.Failure("Email is empty.");

            try
            {
                // Checks against the precompiled regex.
                if (!EmailRegex.IsMatch(email))
                    return Result.Failure("Email syntax is invalid.");

                // Syntax is valid.
                return Result.Success();
            }
            catch (Exception ex) 
            {
                // Logs? Should ideally log here. For now, just returns failure.
                return Result.Failure("Syntax validation error: " + ex.Message);
            }
        }

        /// <summary>
        /// Asynchronously checks if the domain part of an email address has valid Mail Exchanger (MX) or Address (A) DNS records.
        /// </summary>
        /// <param name="email">The full email address.</param>
        /// <returns>
        /// A <see cref="Task{TResult}"/> representing the asynchronous operation, containing a
        /// <see cref="Result"/> indicating success if valid MX or A records are found, or failure otherwise.
        /// </returns>
        private static async Task<Result> HasValidMxRecordAsync(string email)
        {
            try
            {
                // Extracts the domain part from the email address.
                int atIndex = email.LastIndexOf('@');
                // Basic check for valid format before substring.
                if (atIndex < 0 || atIndex >= email.Length - 1)
                    return Result.Failure("Email format error (cannot extract domain).");

                var domain = email.Substring(atIndex + 1);

                // Converts internationalized domain names (IDN) to Punycode (ASCII) for DNS lookup.
                var idn = new IdnMapping();
                domain = idn.GetAscii(domain);

                // Uses DnsClient for DNS queries.
                var lookup = new LookupClient();
                // Queries for MX records.
                var mxResult = await lookup.QueryAsync(domain, QueryType.MX);

                // Returns success immediately if any MX records are found.
                if (mxResult.Answers.MxRecords().Any())
                    return Result.Success();

                // Fallback: If no MX records, checks for A records as some domains might accept email directly.
                var aResult = await lookup.QueryAsync(domain, QueryType.A);
                // Returns success if any A records are found, otherwise failure.
                return aResult.Answers.ARecords().Any()
                    ? Result.Success()
                    : Result.Failure("Domain does not have valid MX or A DNS records.");
            }
            catch (Exception ex) 
            {
                return Result.Failure("DNS lookup error: " + ex.Message);
            }
        }
    }
}