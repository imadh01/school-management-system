using FluentValidation;
using SchoolManagement.Application.DTOs.Students;

namespace SchoolManagement.Application.Validators;

public class CreateStudentFromAdmissionRequestValidator : AbstractValidator<CreateStudentFromAdmissionRequest>
{
    // No required fields — every value here is genuinely optional
    // supplementary profile data, nothing more to validate.
}
