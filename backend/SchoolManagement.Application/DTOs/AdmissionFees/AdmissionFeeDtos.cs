namespace SchoolManagement.Application.DTOs.AdmissionFees;

/// <summary>One class and its admission fee (Amount null = no fee set yet).</summary>
public record AdmissionFeeResponse(string ClassName, decimal? Amount);

public record SetAdmissionFeeRequest(decimal Amount);

/// <summary>The standard fee for the class an application is for, to pre-fill Confirm Admission.</summary>
public record SuggestedAdmissionFeeResponse(string ClassName, decimal? Amount);
