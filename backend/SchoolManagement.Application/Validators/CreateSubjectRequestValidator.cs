using FluentValidation;
using SchoolManagement.Application.DTOs.Subjects;

namespace SchoolManagement.Application.Validators;

public class CreateSubjectRequestValidator : AbstractValidator<CreateSubjectRequest>
{
    private static readonly string[] ValidTypes = { "Theory", "Practical", "Both" };

    public CreateSubjectRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Code).MaximumLength(20);
        RuleFor(x => x.ClassSectionId).GreaterThan(0);
        RuleFor(x => x.Type).NotEmpty().Must(t => ValidTypes.Contains(t))
            .WithMessage("Type must be Theory, Practical, or Both.");

        RuleFor(x => x.MaxMarks).NotNull().When(x => x.Type != "Both")
            .WithMessage("Max Marks is required.");
        RuleFor(x => x.PassMarks).NotNull().When(x => x.Type != "Both")
            .WithMessage("Pass Marks is required.");

        RuleFor(x => x.TheoryMax).NotNull().When(x => x.Type == "Both")
            .WithMessage("Theory Max is required.");
        RuleFor(x => x.TheoryPass).NotNull().When(x => x.Type == "Both")
            .WithMessage("Theory Pass is required.");
        RuleFor(x => x.PracticalMax).NotNull().When(x => x.Type == "Both")
            .WithMessage("Practical Max is required.");
        RuleFor(x => x.PracticalPass).NotNull().When(x => x.Type == "Both")
            .WithMessage("Practical Pass is required.");
    }
}
