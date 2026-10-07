using FluentValidation;
using SchoolManagement.Application.DTOs.Teachers;

namespace SchoolManagement.Application.Validators;

internal static class TeacherRules
{
    public static readonly string[] Statuses = { "Active", "Inactive", "Suspended" };
}

public class CreateTeacherRequestValidator : AbstractValidator<CreateTeacherRequest>
{
    public CreateTeacherRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Username).NotEmpty().MaximumLength(50)
            .Matches("^[a-zA-Z0-9._-]+$")
            .WithMessage("Username can only contain letters, digits, dots, underscores, and hyphens.");
        RuleFor(x => x.Email).NotEmpty().MaximumLength(100).EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MustBeStrongPassword();
        RuleFor(x => x.Phone).MaximumLength(30);
        RuleFor(x => x.Specialization).MaximumLength(100);
        RuleFor(x => x.Status).Must(s => TeacherRules.Statuses.Contains(s))
            .WithMessage("Status must be Active, Inactive or Suspended.");
    }
}

public class UpdateTeacherRequestValidator : AbstractValidator<UpdateTeacherRequest>
{
    public UpdateTeacherRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Username).NotEmpty().MaximumLength(50)
            .Matches("^[a-zA-Z0-9._-]+$")
            .WithMessage("Username can only contain letters, digits, dots, underscores, and hyphens.");
        RuleFor(x => x.Email).NotEmpty().MaximumLength(100).EmailAddress();
        RuleFor(x => x.Phone).MaximumLength(30);
        RuleFor(x => x.Specialization).MaximumLength(100);
        RuleFor(x => x.Status).Must(s => TeacherRules.Statuses.Contains(s))
            .WithMessage("Status must be Active, Inactive or Suspended.");
        RuleFor(x => x.NewPassword!).MustBeStrongPassword()
            .When(x => !string.IsNullOrWhiteSpace(x.NewPassword));
    }
}

public class ChangeTeacherStatusRequestValidator : AbstractValidator<ChangeTeacherStatusRequest>
{
    public ChangeTeacherStatusRequestValidator()
    {
        RuleFor(x => x.Status).Must(s => TeacherRules.Statuses.Contains(s))
            .WithMessage("Status must be Active, Inactive or Suspended.");
    }
}

public class AssignSubjectsRequestValidator : AbstractValidator<AssignSubjectsRequest>
{
    public AssignSubjectsRequestValidator()
    {
        RuleFor(x => x.ClassSectionId).GreaterThan(0);
        RuleFor(x => x.SubjectIds).NotEmpty().WithMessage("Select at least one subject.");
        RuleForEach(x => x.SubjectIds).GreaterThan(0);
    }
}

public class SetClassTeacherRequestValidator : AbstractValidator<SetClassTeacherRequest>
{
    public SetClassTeacherRequestValidator()
    {
        RuleFor(x => x.ClassSectionId).GreaterThan(0);
    }
}
