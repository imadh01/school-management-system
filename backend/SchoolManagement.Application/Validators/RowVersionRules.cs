namespace SchoolManagement.Application.Validators;

public static class RowVersionRules
{
    public const string Message = "RowVersion is required. Reload the record and try again.";

    /// <summary>A SQL Server rowversion is 8 bytes, sent to the browser as base64 text.</summary>
    public static bool IsValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        try { return Convert.FromBase64String(value).Length == 8; }
        catch (FormatException) { return false; }
    }
}
