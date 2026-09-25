using FluentValidation;
using SchoolManagement.Application.DTOs.Parents;

namespace SchoolManagement.Application.Validators;

public class LinkGuardianRequestValidator : AbstractValidator<LinkGuardianRequest>
{
    public LinkGuardianRequestValidator()
    {
        RuleFor(x => x.ParentId).GreaterThan(0);
        RuleFor(x => x.RelationType).Must(r => r is "Father" or "Mother" or "Guardian")
            .WithMessage("RelationType must be 'Father', 'Mother', or 'Guardian'.");
    }
}
