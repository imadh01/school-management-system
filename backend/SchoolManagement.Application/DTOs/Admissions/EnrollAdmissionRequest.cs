namespace SchoolManagement.Application.DTOs.Admissions;

public record EnrollAdmissionRequest(
    string RollNumber, string AdmissionNumber, DateOnly AdmissionDate,
    string? EntryPoint, bool TransportRequired, int AllottedClassSectionId,
    string? Nationality, string? CurriculumTrack, string? EnglishProficiency,
    string? EalCode, string? House, string? Allergies);
