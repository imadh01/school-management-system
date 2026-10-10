using FluentValidation;
using SchoolManagement.Application.DTOs.ClassSections;

namespace SchoolManagement.Application.Validators;

public class UpdateClassSectionRequestValidator : AbstractValidator<UpdateClassSectionRequest>
{
    public UpdateClassSectionRequestValidator()
    {
        RuleFor(x => x.RowVersion).Must(RowVersionRules.IsValid).WithMessage(RowVersionRules.Message);
        RuleFor(x => x.Name).NotEmpty().WithMessage("Class name is required.")
            .MaximumLength(50).WithMessage("Class name cannot exceed 50 characters.");
        RuleFor(x => x.Section).NotEmpty().WithMessage("Section is required.")
            .MaximumLength(10).WithMessage("Section cannot exceed 10 characters.");
        RuleFor(x => x.Grade).InclusiveBetween(1, 12).When(x => x.Grade.HasValue)
            .WithMessage("Grade level must be between 1 and 12.");
        RuleFor(x => x.Stage).Must(v => ClassSectionOptions.Stages.Contains(v))
            .WithMessage("Curriculum stage must be Pre-Primary, Primary or Secondary.");
        RuleFor(x => x.Medium).Must(v => ClassSectionOptions.Mediums.Contains(v))
            .WithMessage("Medium must be English, Hindi or Arabic.");
        RuleFor(x => x.Stream).Must(v => ClassSectionOptions.Streams.Contains(v))
            .WithMessage("Stream must be General, Science or Commerce.");
        RuleFor(x => x.Capacity).InclusiveBetween(1, 200)
            .WithMessage("Max capacity must be between 1 and 200.");
        RuleFor(x => x.Building).MaximumLength(50);
        RuleFor(x => x.Floor).InclusiveBetween(0, 20).When(x => x.Floor.HasValue)
            .WithMessage("Floor must be between 0 and 20.");
        RuleFor(x => x.Room).MaximumLength(30);
    }
}
