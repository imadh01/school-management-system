using FluentValidation;
using SchoolManagement.Application.DTOs.ClassSections;

namespace SchoolManagement.Application.Validators;

public class CreateClassSectionRequestValidator : AbstractValidator<CreateClassSectionRequest>
{
    public CreateClassSectionRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Section).NotEmpty().MaximumLength(10);
        RuleFor(x => x.Grade).GreaterThan(0).When(x => x.Grade.HasValue);
        RuleFor(x => x.Capacity).GreaterThan(0).When(x => x.Capacity.HasValue);
        RuleFor(x => x.Room).MaximumLength(30);
        RuleFor(x => x.AcademicYearId).GreaterThan(0).When(x => x.AcademicYearId.HasValue);
    }
}
