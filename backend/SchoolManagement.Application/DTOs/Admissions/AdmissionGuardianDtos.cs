namespace SchoolManagement.Application.DTOs.Admissions;

/// <summary>
/// A guardian typed onto an application (registration / edit).
/// G2: when <paramref name="ParentId"/> is set, the guardian IS that existing parent. The server copies the parent's
/// name, mobile and email and ignores whatever the form sent for those three fields.
/// </summary>
public record AdmissionGuardianRequest(
    string RelationType, string Name, string? Mobile, string? Email, bool IsPrimaryContact, int? ParentId = null);

public record AdmissionGuardianResponse(
    int Id, string RelationType, string Name, string? Mobile, string? Email, bool IsPrimaryContact, int? ParentId = null);

/// <summary>What staff decided to do with one guardian at enrolment.</summary>
public static class GuardianActions
{
    public const string UseExisting = "UseExisting";   // link to an existing Parent (ParentId required)
    public const string CreateNew = "CreateNew";       // create a new Parent from the guardian

    // Suggestions returned by the matches endpoint (never accepted as a decision):
    public const string MustChoose = "MustChoose";         // several parents share the number
    public const string MissingMobile = "MissingMobile";   // cannot enrol until a mobile is added
    public const string ParentRemoved = "ParentRemoved";   // G2: the linked parent was deleted; unlink or choose another
}

public record GuardianDecision(int AdmissionGuardianId, string Action, int? ParentId);

public record ParentCandidateResponse(
    int ParentId, string Name, string Mobile, string? Email, List<string> LinkedChildren);

/// <param name="LinkedParentId">G2: the parent chosen at registration (null if none). When it is still a live
/// parent it is the first candidate and the suggestion is UseExisting.</param>
public record GuardianMatchResponse(
    int AdmissionGuardianId, string RelationType, string Name, string? Mobile,
    string SuggestedAction, List<ParentCandidateResponse> Candidates, int? LinkedParentId = null);

public record GuardianMatchesResponse(int AdmissionId, List<GuardianMatchResponse> Guardians);
