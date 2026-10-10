using FluentValidation;
using SchoolManagement.Application.DTOs.Admissions;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Application.Validators;

public class UpdateAdmissionRequestValidator : AbstractValidator<UpdateAdmissionRequest>
{
    public UpdateAdmissionRequestValidator()
    {
        RuleFor(x => x.RowVersion).Must(RowVersionRules.IsValid).WithMessage(RowVersionRules.Message);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(50);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Gender).NotEmpty();
        RuleFor(x => x.DateOfBirth).LessThan(DateOnly.FromDateTime(DateTime.UtcNow));
        RuleFor(x => x.AppliedForClassSectionId).GreaterThan(0);
        RuleFor(x => x.AdmissionType).Must(AdmissionTypes.IsValid)
            .WithMessage("AdmissionType must be 'New' or 'Transfer'.");
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrEmpty(x.Email));
        RuleFor(x => x.Remarks).MaximumLength(300);

        RuleForEach(x => x.Guardians).SetValidator(new AdmissionGuardianRequestValidator());
        RuleFor(x => x.Guardians).Must(AdmissionGuardianListRules.WithinLimit)
            .WithMessage($"An application can have at most {AdmissionGuardianListRules.MaxGuardians} guardians.");
        RuleFor(x => x.Guardians).Must(AdmissionGuardianListRules.AtMostOnePrimary)
            .WithMessage("Only one guardian can be the primary contact.");
        RuleFor(x => x.Guardians).Must(AdmissionGuardianListRules.AtMostOneFatherAndMother)
            .WithMessage("An application can list only one father and one mother.");
    }
}
