using FluentValidation;
using SchoolManagement.Application.DTOs.Admissions;

namespace SchoolManagement.Application.Validators;

public class GuardianDecisionValidator : AbstractValidator<GuardianDecision>
{
    public GuardianDecisionValidator()
    {
        RuleFor(x => x.AdmissionGuardianId).GreaterThan(0);
        RuleFor(x => x.Action).Must(a => a is GuardianActions.UseExisting or GuardianActions.CreateNew)
            .WithMessage("Action must be 'UseExisting' or 'CreateNew'.");
        RuleFor(x => x.ParentId).NotNull().GreaterThan(0)
            .When(x => x.Action == GuardianActions.UseExisting)
            .WithMessage("ParentId is required when Action is 'UseExisting'.");
        RuleFor(x => x.ParentId).Null()
            .When(x => x.Action == GuardianActions.CreateNew)
            .WithMessage("ParentId must be empty when Action is 'CreateNew'.");
    }
}

public class EnrollAdmissionRequestValidator : AbstractValidator<EnrollAdmissionRequest>
{
    public EnrollAdmissionRequestValidator()
    {
        RuleFor(x => x.RollNumber).NotEmpty().MaximumLength(20);
        RuleFor(x => x.AdmissionNumber).NotEmpty().MaximumLength(30);
        RuleFor(x => x.AllottedClassSectionId).GreaterThan(0);

        RuleFor(x => x.EntryPoint).MaximumLength(50);
        RuleFor(x => x.Nationality).MaximumLength(50);
        RuleFor(x => x.CurriculumTrack).MaximumLength(50);
        RuleFor(x => x.EnglishProficiency).MaximumLength(30);
        RuleFor(x => x.EalCode).MaximumLength(30);
        RuleFor(x => x.House).MaximumLength(30);
        RuleFor(x => x.Allergies).MaximumLength(300);

        // The decisions list itself is checked against the real guardians in AdmissionService.
        RuleFor(x => x.Guardians).NotNull().WithMessage("Guardian decisions are required.");
        RuleForEach(x => x.Guardians).SetValidator(new GuardianDecisionValidator());
        RuleFor(x => x.Guardians)
            .Must(g => g is null || g.Select(d => d.AdmissionGuardianId).Distinct().Count() == g.Count)
            .WithMessage("Each guardian can have only one decision.");
    }
}