using FluentValidation;
using SchoolManagement.Application.DTOs.Subjects;

namespace SchoolManagement.Application.Validators;

public class UpdateSubjectRequestValidator : AbstractValidator<UpdateSubjectRequest>
{
    private static readonly string[] ValidTypes = { "Theory", "Practical", "Both" };

    public UpdateSubjectRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Code).MaximumLength(20);
        RuleFor(x => x.ClassSectionId).GreaterThan(0);
        RuleFor(x => x.Type).NotEmpty().Must(t => ValidTypes.Contains(t))
            .WithMessage("Type must be Theory, Practical, or Both.");

        RuleFor(x => x.MaxMarks).NotNull().When(x => x.Type != "Both");
        RuleFor(x => x.PassMarks).NotNull().When(x => x.Type != "Both");

        RuleFor(x => x.TheoryMax).NotNull().When(x => x.Type == "Both");
        RuleFor(x => x.TheoryPass).NotNull().When(x => x.Type == "Both");
        RuleFor(x => x.PracticalMax).NotNull().When(x => x.Type == "Both");
        RuleFor(x => x.PracticalPass).NotNull().When(x => x.Type == "Both");
    }
}