namespace SchoolManagement.API.Common;

/// <summary>
/// Matches the error envelope from the master prompt's error-handling
/// convention. TraceId is HttpContext.TraceIdentifier for now — the
/// real correlation-ID middleware (Error Handling step) will populate
/// this with something more meaningful later.
/// </summary>
public class ApiErrorResponse
{
    public bool Success { get; init; } = false;
    public required string Message { get; init; }
    public required string ErrorCode { get; init; }
    public IDictionary<string, string[]>? Errors { get; init; }
    public string? TraceId { get; init; }
}
