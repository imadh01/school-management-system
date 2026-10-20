using FluentValidation;
using SchoolManagement.Application.DTOs.AcademicYears;

namespace SchoolManagement.Application.Validators;

public class CreateAcademicYearRequestValidator : AbstractValidator<CreateAcademicYearRequest>
{
    public CreateAcademicYearRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required.")
            .MaximumLength(20).WithMessage("Name cannot exceed 20 characters.");
        RuleFor(x => x.StartDate).NotEqual(default(DateOnly)).WithMessage("Start date is required.");
        RuleFor(x => x.EndDate).NotEqual(default(DateOnly)).WithMessage("End date is required.")
            .GreaterThan(x => x.StartDate).WithMessage("End date must be after the start date.");
    }
}

public class UpdateAcademicYearRequestValidator : AbstractValidator<UpdateAcademicYearRequest>
{
    public UpdateAcademicYearRequestValidator()
    {
        RuleFor(x => x.RowVersion).Must(RowVersionRules.IsValid).WithMessage(RowVersionRules.Message);
        RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required.")
            .MaximumLength(20).WithMessage("Name cannot exceed 20 characters.");
        RuleFor(x => x.StartDate).NotEqual(default(DateOnly)).WithMessage("Start date is required.");
        RuleFor(x => x.EndDate).NotEqual(default(DateOnly)).WithMessage("End date is required.")
            .GreaterThan(x => x.StartDate).WithMessage("End date must be after the start date.");
    }
}

public class SetCurrentAcademicYearRequestValidator : AbstractValidator<SetCurrentAcademicYearRequest>
{
    public SetCurrentAcademicYearRequestValidator()
    {
        RuleFor(x => x.RowVersion).Must(RowVersionRules.IsValid).WithMessage(RowVersionRules.Message);
    }
}

public class CloseAcademicYearRequestValidator : AbstractValidator<CloseAcademicYearRequest>
{
    public CloseAcademicYearRequestValidator()
    {
        RuleFor(x => x.RowVersion).Must(RowVersionRules.IsValid).WithMessage(RowVersionRules.Message);
        RuleFor(x => x.ConfirmName).NotEmpty().WithMessage("Type the academic year name to confirm.");
    }
}
