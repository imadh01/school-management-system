using System.Text.RegularExpressions;

namespace SchoolManagement.Application.Validators;

/// <summary>One definition of an acceptable phone/mobile format for guardians and pickup persons.</summary>
public static class PhoneNumberRules
{
    private static readonly Regex Pattern = new(@"^\+?[0-9\s\-().]{5,20}$", RegexOptions.Compiled);

    public const string Message = "Phone number is invalid (digits, spaces, + - ( ) only, 5–20 characters).";

    public static bool IsValid(string? value) => value is not null && Pattern.IsMatch(value.Trim());
}
