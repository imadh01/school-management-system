namespace SchoolManagement.Application.DTOs.Parents;

public record LinkedStudentResponse(
    int StudentId, string StudentName, string AdmNo, string ClassSectionName,
    string RollNumber, string RelationType, bool IsPrimaryContact);