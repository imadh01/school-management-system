using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Application.Services;

/// <summary>
/// The shape of the numbers the system generates at enrolment, in one place:
///   admission number  ADM-{year}-{4-digit serial}      e.g. ADM-2026-0001  (serial restarts every year)
///   roll number       {class}{section}{3-digit serial}  e.g. 5A001, 10B012   (serial counts within the section)
/// </summary>
public static class NumberFormats
{
    public static string AdmissionPrefix(int year) => $"ADM-{year}-";

    public static string AdmissionNumber(int year, int serial) => $"{AdmissionPrefix(year)}{serial:0000}";

    /// <summary>"Class 5" + "A" -> "5A"; "LKG" + "B" -> "LKGB".</summary>
    public static string RollPrefix(ClassSection classSection)
    {
        var name = classSection.Name.Trim();
        if (name.StartsWith("Class ", StringComparison.OrdinalIgnoreCase)) name = name[6..];
        return Letters(name) + Letters(classSection.Section);
    }

    public static string RollNumber(string prefix, int serial) => $"{prefix}{serial:000}";

    /// <summary>Staff and teachers share one series: EMP-0001, EMP-0002 ...</summary>
    public const string EmployeePrefix = "EMP-";

    public static string EmployeeNumber(int serial) => $"{EmployeePrefix}{serial:0000}";

    /// <summary>The highest serial among values that are exactly <paramref name="prefix"/> + digits; 0 when none.</summary>
    public static int MaxSerial(IEnumerable<string> values, string prefix)
    {
        var max = 0;
        foreach (var value in values)
        {
            if (!value.StartsWith(prefix, StringComparison.Ordinal)) continue;
            var tail = value[prefix.Length..];
            if (tail.Length > 0 && tail.All(char.IsDigit) && int.TryParse(tail, out var serial) && serial > max)
                max = serial;
        }
        return max;
    }

    private static string Letters(string text) =>
        new string(text.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
}
