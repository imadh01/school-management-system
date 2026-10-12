namespace SchoolManagement.Application.DTOs.Students;

// Batch F1: what a caller with only Students.ViewScoped receives (a teacher, and later a parent or
// student). Every property keeps the SAME name as in the full response, so these are strict subsets:
// the UI reads the same keys and simply finds fewer of them.
//
// Left out on purpose (full-View callers only): contact details and address, category, religion,
// nationality, custody arrangement, fee concession, admission data, identity documents, pickup persons
// and all health fields. Allergies appear only as a yes/no flag (the text needs Students.ViewSensitive,
// which a scoped read never implies).

/// <summary>One row of the student list for a scoped caller.</summary>
public record StudentScopedSummaryResponse(
    int Id, string AdmNo, string RollNumber, int ClassSectionId, string ClassSectionName,
    string Status, string? PhotoUrl,
    string FirstName, string? MiddleName, string LastName, string Gender, DateOnly DateOfBirth,
    string? House, bool HasAllergies);

/// <summary>Only whether the student has allergies: a safety flag for class. The details need Students.ViewSensitive.</summary>
public record StudentScopedHealthResponse(bool HasAllergies);

/// <summary>A guardian as a scoped caller sees them: who to call, nothing more.</summary>
public record GuardianContactResponse(
    int ParentId, string ParentName, string ParentMobile, string RelationType, bool IsPrimaryContact);

/// <summary>
/// One student for a scoped caller. <see cref="Guardians"/> holds every guardian for a teacher (or the
/// student themself), but only the caller's own link for a parent: co-guardians stay private
/// (custody arrangements exist for a reason).
/// </summary>
public record StudentScopedResponse(
    int Id, string AdmNo, string RollNumber, int ClassSectionId, string ClassSectionName,
    string Status, string? PhotoUrl,
    string FirstName, string? MiddleName, string LastName, string? PreferredName,
    string Gender, DateOnly DateOfBirth, string? House,
    StudentScopedHealthResponse Health,
    List<GuardianContactResponse> Guardians);
