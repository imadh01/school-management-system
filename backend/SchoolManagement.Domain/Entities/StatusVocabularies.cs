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
