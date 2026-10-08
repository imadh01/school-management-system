using FluentValidation;
using SchoolManagement.Application.DTOs.Students;

namespace SchoolManagement.Application.Validators;

public class StudentHealthDtoValidator : AbstractValidator<StudentHealthDto>
{
    public StudentHealthDtoValidator()
    {
        RuleFor(x => x.BloodGroup).MaximumLength(10);
        RuleFor(x => x.Allergies).MaximumLength(300);
        RuleFor(x => x.DietaryRequirements).MaximumLength(200);
        RuleFor(x => x.MedicalNotes).MaximumLength(500);
        RuleFor(x => x.SpecialEducationalNeeds).MaximumLength(500);
        RuleFor(x => x.InsuranceProvider).MaximumLength(100);
    }
}

public class PickupPersonRequestValidator : AbstractValidator<PickupPersonRequest>
{
    public PickupPersonRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Relation).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Phone).NotEmpty().Must(PhoneNumberRules.IsValid).WithMessage(PhoneNumberRules.Message);
        RuleFor(x => x.IdNote).MaximumLength(100);
    }
}

public class UpdateStudentIdentityRequestValidator : AbstractValidator<UpdateStudentIdentityRequest>
{
    public UpdateStudentIdentityRequestValidator()
    {
        // Spaces are allowed on input ("1234 5678 9012") and stripped by the service.
        RuleFor(x => x.AadhaarNumber)
            .Must(a => a!.Replace(" ", "").Length == 12 && a.Replace(" ", "").All(char.IsAsciiDigit))
            .When(x => !string.IsNullOrWhiteSpace(x.AadhaarNumber))
            .WithMessage("Aadhaar number must be exactly 12 digits.");
        RuleFor(x => x.PassportNumber).MaximumLength(30);
        RuleFor(x => x.VisaType).MaximumLength(30);
    }
}

/// <summary>Length/shape rules shared by the create and update student validators.</summary>
public static class StudentRequestRules
{
    public const int MaxPickupPersons = 10; // technical cap, not a business rule

    public static bool PickupPersonsWithinLimit(List<PickupPersonRequest>? p) => p is null || p.Count <= MaxPickupPersons;
}