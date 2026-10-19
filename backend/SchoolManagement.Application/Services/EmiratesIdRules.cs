namespace SchoolManagement.Application.Services;

/// <summary>
/// The UAE Emirates ID: 15 digits, always starting with 784 (the UAE's country code), written
/// 784-YYYY-NNNNNNN-C. Typed with or without dashes and spaces; stored as the 15 digits.
/// </summary>
public static class EmiratesIdRules
{
    public const string Message = "Emirates ID must be 15 digits starting with 784 (for example 784-1990-1234567-1).";

    /// <summary>Digits only; null for blank.</summary>
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return new string(value.Where(c => c != '-' && !char.IsWhiteSpace(c)).ToArray());
    }

    public static bool IsValid(string? value)
    {
        var digits = Normalize(value);
        return digits is null || (digits.Length == 15 && digits.All(char.IsAsciiDigit) && digits.StartsWith("784", StringComparison.Ordinal));
    }

    /// <summary>"784199012345671" -> "784-1990-1234567-1". Anything else is returned unchanged.</summary>
    public static string? Format(string? digits) =>
        digits is { Length: 15 } ? $"{digits[..3]}-{digits[3..7]}-{digits[7..14]}-{digits[14..]}" : digits;
}
