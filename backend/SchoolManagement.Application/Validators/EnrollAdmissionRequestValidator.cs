using FluentValidation;
using SchoolManagement.Application.DTOs.Admissions;

namespace SchoolManagement.Application.Validators;

public class EnrollAdmissionRequestValidator : AbstractValidator<EnrollAdmissionRequest>
{
    public EnrollAdmissionRequestValidator()
    {
        RuleFor(x => x.RollNumber).NotEmpty().MaximumLength(20);
        RuleFor(x => x.AdmissionNumber).NotEmpty().MaximumLength(30);
        RuleFor(x => x.AllottedClassSectionId).GreaterThan(0);
    }
}
