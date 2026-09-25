namespace SchoolManagement.Application.DTOs.Admissions;

public record ConfirmAdmissionRequest(
    decimal? AdmissionFee, string? AdmissionFeeReference,
    string? BloodGroup, string? Religion, string? Category,
    string? MedicalNotes, string? Remarks);