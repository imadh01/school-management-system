namespace SchoolManagement.Application.DTOs.Admissions;

public record EnrollAdmissionRequest(
    string RollNumber, string AdmissionNumber, DateOnly AdmissionDate,
    string? EntryPoint, bool TransportRequired, int AllottedClassSectionId);