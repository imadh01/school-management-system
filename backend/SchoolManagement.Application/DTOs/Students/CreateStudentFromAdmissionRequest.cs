namespace SchoolManagement.Application.DTOs.Students;

public record CreateStudentFromAdmissionRequest(
    string? Nationality, string? CurriculumTrack,
    string? EnglishProficiency, string? EalCode, string? House, string? Allergies);