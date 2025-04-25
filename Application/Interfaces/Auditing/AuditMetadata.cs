namespace ArandanoIRT_Backend.Application.Interfaces.Auditing
{
    /// <summary>
    /// Represents metadata associated with an audited event or action.
    /// </summary>
    /// <param name="UserId">The unique identifier of the user who performed the action, if available.</param>
    /// <param name="IpAddress">The IP address from which the action originated, if available.</param>
    /// <param name="UserAgent">The user agent string of the client that performed the action, if available.</param>
    /// <param name="PerformedAt">The date and time (usually UTC) when the action was performed.</param>
    public record AuditMetadata(
        int? UserId,
        string? IpAddress,
        string? UserAgent,
        DateTime PerformedAt
    );
}