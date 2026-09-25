namespace SchoolManagement.Application.DTOs.Parents;

public record LinkStudentRequest(int StudentId, string RelationType, bool IsPrimaryContact);
