namespace SchoolManagement.Application.DTOs.Parents;

public record StudentGuardianResponse(
    int ParentId, string ParentName, string ParentMobile, string? ParentEmail,
    string RelationType, bool IsPrimaryContact);