namespace SchoolManagement.Domain.Constants;

/// <summary>
/// Names of the six seeded (system) roles. Code that needs a specific role refers to these
/// constants instead of repeating the string.
/// </summary>
public static class RoleNames
{
    public const string Admin = "Admin";
    public const string Supervisor = "Supervisor";
    public const string Clerk = "Clerk";
    public const string Teacher = "Teacher";
    public const string Student = "Student";
    public const string Parent = "Parent";

    public static readonly IReadOnlyList<string> All = new[] { Admin, Supervisor, Clerk, Teacher, Student, Parent };
}
