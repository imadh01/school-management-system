using FluentValidation;
using SchoolManagement.Application.DTOs.Admissions;

namespace SchoolManagement.Application.Validators;

public class UpdateAdmissionRequestValidator : AbstractValidator<UpdateAdmissionRequest>
{
    public UpdateAdmissionRequestValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(50);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Gender).NotEmpty();
        RuleFor(x => x.DateOfBirth).LessThan(DateOnly.FromDateTime(DateTime.UtcNow));
        RuleFor(x => x.AppliedForClassSectionId).GreaterThan(0);
        RuleFor(x => x.AdmissionType).Must(t => t is "New" or "Transfer")
            .WithMessage("AdmissionType must be 'New' or 'Transfer'.");
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrEmpty(x.Email));
    }
}