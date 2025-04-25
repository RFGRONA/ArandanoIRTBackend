using System.Globalization;
using System.Text.RegularExpressions;
using DnsClient;
using ArandanoIRT_Backend.Domain.ValueObjects; // Assuming Result is here

namespace ArandanoIRT_Backend.Application.Utilities
{
    /// <summary>
    /// Provides static utility methods for validating email addresses,
    /// including syntax checks and DNS record verification (MX/A).
    /// </summary>
    public static partial class EmailValidatorUtility
    {
        /// <summary>
        /// Provides a compiled regular expression instance generated at compile time
        /// for validating the basic syntax of an email address.
        /// </summary>
        /// <remarks>
        /// Allows letters, numbers, underscores, plus signs, dots, hyphens in the local part,
        /// but prevents consecutive dots and dots/hyphens at boundaries where not logical.
        /// Uses IgnoreCase option.
        /// </remarks>
        [GeneratedRegex(
            @"^(?!.*\.\.)[a-zA-Z0-9_+]+([.-]?[a-zA-Z0-9_+]+)*@[a-zA-Z0-9]+([.-]?[a-zA-Z0-9]+)*\.[a-zA-Z]{2,}$",
            RegexOptions.IgnoreCase
        )]
        private static partial Regex EmailRegex();


        /// <summary>
        /// Asynchronously validates an email address for correct syntax and checks for valid DNS (MX or A) records for its domain.
        /// </summary>
        /// <param name="email">The email address to validate.</param>
        /// <returns>
        /// A <see cref="Task{TResult}"/> representing the asynchronous operation, containing a
        /// <see cref="Result"/> object. The result indicates success if the email is valid,
        /// or failure with an error message if validation fails at any step (syntax or DNS).
        /// </returns>
        public static async Task<Result> ValidateEmailAsync(string? email) // Allow nullable input
        {
            // 1. Validates email syntax first.
            var syntaxResult = IsValidEmailSyntax(email);
            if (syntaxResult.IsFailure)
                return syntaxResult;

            // Email is non-null and syntax is valid here, safe to use '!'
            // 2. Validates DNS records (MX/A) for the domain.
            return await HasValidMxRecordAsync(email!);
        }

        /// <summary>
        /// Validates the basic syntax of an email address using a regular expression.
        /// </summary>
        /// <param name="email">The email address string to check.</param>
        /// <returns>A <see cref="Result"/> indicating success or failure for syntax validation.</returns>
        private static Result IsValidEmailSyntax(string? email) // Allow nullable input
        {
            if (string.IsNullOrWhiteSpace(email))
                return Result.Failure("Email is empty.");

            try
            {
                if (!EmailRegex().IsMatch(email))
                    return Result.Failure("Email syntax is invalid.");

                // Syntax is valid.
                return Result.Success();
            }
            catch (Exception ex)
            {
                return Result.Failure($"Syntax validation error: {ex.Message}");
            }
        }

        /// <summary>
        /// Asynchronously checks if the domain part of an email address has valid Mail Exchanger (MX) or Address (A) DNS records.
        /// </summary>
        /// <param name="email">The full email address (non-null and syntactically valid at this point).</param>
        /// <returns>
        /// A <see cref="Task{TResult}"/> representing the asynchronous operation, containing a
        /// <see cref="Result"/> indicating success if valid MX or A records are found, or failure otherwise.
        /// </returns>
        private static async Task<Result> HasValidMxRecordAsync(string email)
        {
            string domain;
            try
            {
                int atIndex = email.LastIndexOf('@');
                if (atIndex < 0 || atIndex >= email.Length - 1)
                    return Result.Failure("Email format error (cannot extract domain).");

                domain = email.Substring(atIndex + 1);

                var idn = new IdnMapping();
                domain = idn.GetAscii(domain);
            }
            catch (ArgumentException argEx)
            {
                return Result.Failure($"Invalid domain format: {argEx.Message}");
            }
            catch (Exception ex)
            {
                return Result.Failure($"Error processing domain part: {ex.Message}");
            }

            try
            {
                var lookup = new LookupClient();
                var mxResult = await lookup.QueryAsync(domain, QueryType.MX);

                if (mxResult.Answers.MxRecords().Any())
                    return Result.Success();

                var aResult = await lookup.QueryAsync(domain, QueryType.A);
                return aResult.Answers.ARecords().Any()
                    ? Result.Success()
                    : Result.Failure("Domain does not have valid MX or A DNS records.");
            }
            catch (DnsResponseException)
            {
                return Result.Failure($"DNS lookup failed for domain '{domain}'.");
            }
            catch (Exception ex)
            {
                return Result.Failure($"DNS lookup error: {ex.Message}");
            }
        }
    }
}