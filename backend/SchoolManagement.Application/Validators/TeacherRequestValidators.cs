using FluentValidation;
using SchoolManagement.Application.DTOs.Teachers;

namespace SchoolManagement.Application.Validators;

public class UpdateTeacherRequestValidator : AbstractValidator<UpdateTeacherRequest>
{
    public UpdateTeacherRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        // Same rule as the staff profile's mobile, which this phone is copied to.
        RuleFor(x => x.Phone).Must(UserRequestRules.IsOptionalPhone).WithMessage(PhoneNumberRules.Message);
        RuleFor(x => x.Specialization).MaximumLength(100);
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
