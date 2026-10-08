using FluentValidation;
using SchoolManagement.Application.DTOs.Admissions;

namespace SchoolManagement.Application.Validators;

public class AdmissionGuardianRequestValidator : AbstractValidator<AdmissionGuardianRequest>
{
    public AdmissionGuardianRequestValidator()
    {
        RuleFor(x => x.RelationType).Must(r => r is "Father" or "Mother" or "Guardian")
            .WithMessage("RelationType must be 'Father', 'Mother', or 'Guardian'.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);

        // Mobile is optional on a draft application; enrolment is blocked until it exists.
        RuleFor(x => x.Mobile).Must(PhoneNumberRules.IsValid)
            .When(x => !string.IsNullOrWhiteSpace(x.Mobile))
            .WithMessage(PhoneNumberRules.Message);
        RuleFor(x => x.Email).EmailAddress().MaximumLength(100)
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}

/// <summary>Rules about the guardian list as a whole, shared by the create and update validators.</summary>
public static class AdmissionGuardianListRules
{
    public const int MaxGuardians = 5; // technical cap, not a business rule

    public static bool WithinLimit(List<AdmissionGuardianRequest>? g) => g is null || g.Count <= MaxGuardians;
    public static bool AtMostOnePrimary(List<AdmissionGuardianRequest>? g) =>
        g is null || g.Count(x => x.IsPrimaryContact) <= 1;
    public static bool AtMostOneFatherAndMother(List<AdmissionGuardianRequest>? g) =>
        g is null || (g.Count(x => x.RelationType == "Father") <= 1 && g.Count(x => x.RelationType == "Mother") <= 1);
}