namespace SchoolManagement.Application.DTOs.Parents;

public record LinkGuardianRequest(int ParentId, string RelationType, bool IsPrimaryContact);