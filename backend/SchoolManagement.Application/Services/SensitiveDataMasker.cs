namespace SchoolManagement.Application.Services;

/// <summary>Masks identity numbers for callers without Students.ViewSensitive.</summary>
public static class SensitiveDataMasker
{
    /// <summary>"123456789012" → "********9012". Short values are fully masked.</summary>
    public static string? MaskTail(string? value, int visible = 4)
    {
        if (string.IsNullOrEmpty(value)) return value;
        if (value.Length <= visible) return new string('*', value.Length);
        return new string('*', value.Length - visible) + value[^visible..];
    }
}