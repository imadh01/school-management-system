namespace SchoolManagement.Domain.Entities;

// One definition of every allowed status / type value. The database CHECK constraints,
// the FluentValidation rules and the services all read these lists, so they cannot drift apart.
// (The React forms still hold their own copy of the labels; keep them in step when you change a list.)

/// <summary>How an applicant arrives: a new joiner or a transfer from another school.</summary>
public static class AdmissionTypes
{
    public const string New = "New";
    public const string Transfer = "Transfer";

    public static readonly string[] All = { New, Transfer };

    public static bool IsValid(string? value) => value is not null && All.Contains(value);
}

/// <summary>G1a: the kinds of address a person can have, at most one of each.</summary>
public static class AddressTypes
{
    public const string Home = "Home";
    public const string Permanent = "Permanent";
    public const string Mailing = "Mailing";

    public static readonly string[] All = { Home, Permanent, Mailing };

    public static bool IsValid(string? value) => value is not null && All.Contains(value);
}

/// <summary>G1b: review state of an uploaded document. Unverify sends a Verified document back to Pending.</summary>
public static class DocumentStatuses
{
    public const string Pending = "Pending";
    public const string Verified = "Verified";
    public const string Rejected = "Rejected";

    public static readonly string[] All = { Pending, Verified, Rejected };

    public static bool IsValid(string? value) => value is not null && All.Contains(value);
}

/// <summary>
/// G1b: the only file types accepted for documents, as detected from the file's own bytes.
/// <see cref="Extensions"/> gives the extension the stored and downloaded file gets.
/// </summary>
public static class DocumentContentTypes
{
    public const string Pdf = "application/pdf";
    public const string Jpeg = "image/jpeg";
    public const string Png = "image/png";

    public static readonly string[] All = { Pdf, Jpeg, Png };

    public static readonly IReadOnlyDictionary<string, string> Extensions = new Dictionary<string, string>
    {
        [Pdf] = ".pdf",
        [Jpeg] = ".jpg",
        [Png] = ".png",
    };
}

/// <summary>Admission pipeline: Registered -> Admitted -> Enrolled, or Rejected.</summary>
public static class AdmissionStatuses
{
    public const string Registered = "Registered";
    public const string Admitted = "Admitted";
    public const string Enrolled = "Enrolled";
    public const string Rejected = "Rejected";

    public static readonly string[] All = { Registered, Admitted, Enrolled, Rejected };
}

/// <summary>Student-level status (the academic outcome of each period lives in EnrollmentStatuses).</summary>
public static class StudentStatuses
{
    public const string Active = "Active";
    public const string Inactive = "Inactive";
    public const string Left = "Left";

    public static readonly string[] All = { Active, Inactive, Left };

    public static bool IsValid(string? value) => value is not null && All.Contains(value);
}

/// <summary>
/// Login account status (CK_Users_Status). Anything but Active blocks sign-in and ends every session:
/// Inactive = left the school, Suspended = temporarily blocked.
/// </summary>
public static class UserStatuses
{
    public const string Active = "Active";
    public const string Inactive = "Inactive";
    public const string Suspended = "Suspended";

    public static readonly string[] All = { Active, Inactive, Suspended };

    public static bool IsValid(string? value) => value is not null && All.Contains(value);
}

/// <summary>E2: genders a staff profile can have (CK_StaffProfiles_Gender). Same values as the student forms.</summary>
public static class Genders
{
    public const string Male = "Male";
    public const string Female = "Female";

    public static readonly string[] All = { Male, Female };

    public static bool IsValid(string? value) => value is not null && All.Contains(value);
}

/// <summary>Simple on/off status used by class sections, subjects and parents.</summary>
public static class ActiveStatuses
{
    public const string Active = "Active";
    public const string Inactive = "Inactive";

    public static readonly string[] All = { Active, Inactive };

    public static bool IsValid(string? value) => value is not null && All.Contains(value);
}

/// <summary>Column widths shared by tables that copy values between each other at enrolment.</summary>
public static class FieldLimits
{
    /// <summary>Admissions.Category and Students.Category (the value is copied at enrolment).</summary>
    public const int Category = 50;
    /// <summary>Admissions.AdmissionType and Students.AdmissionType.</summary>
    public const int AdmissionType = 20;
}
