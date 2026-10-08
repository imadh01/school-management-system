namespace SchoolManagement.Application.DTOs.Admissions;

/// <summary>A guardian typed onto an application (registration / edit).</summary>
public record AdmissionGuardianRequest(
    string RelationType, string Name, string? Mobile, string? Email, bool IsPrimaryContact);

public record AdmissionGuardianResponse(
    int Id, string RelationType, string Name, string? Mobile, string? Email, bool IsPrimaryContact);

/// <summary>What staff decided to do with one guardian at enrolment.</summary>
public static class GuardianActions
{
    public const string UseExisting = "UseExisting";   // link to an existing Parent (ParentId required)
    public const string CreateNew = "CreateNew";       // create a new Parent from the guardian

    // Suggestions returned by the matches endpoint (never accepted as a decision):
    public const string MustChoose = "MustChoose";         // several parents share the number
    public const string MissingMobile = "MissingMobile";   // cannot enrol until a mobile is added
}

public record GuardianDecision(int AdmissionGuardianId, string Action, int? ParentId);

public record ParentCandidateResponse(
    int ParentId, string Name, string Mobile, string? Email, List<string> LinkedChildren);

public record GuardianMatchResponse(
    int AdmissionGuardianId, string RelationType, string Name, string? Mobile,
    string SuggestedAction, List<ParentCandidateResponse> Candidates);

public record GuardianMatchesResponse(int AdmissionId, List<GuardianMatchResponse> Guardians);
