using FluentValidation;
using SchoolManagement.Application.DTOs.Parents;

namespace SchoolManagement.Application.Validators;

public class LinkStudentRequestValidator : AbstractValidator<LinkStudentRequest>
{
    public LinkStudentRequestValidator()
    {
        RuleFor(x => x.StudentId).GreaterThan(0);
        RuleFor(x => x.RelationType).Must(r => r is "Father" or "Mother" or "Guardian")
            .WithMessage("RelationType must be 'Father', 'Mother', or 'Guardian'.");
    }
}
