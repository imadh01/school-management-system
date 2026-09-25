using FluentValidation;
using SchoolManagement.Application.DTOs.Parents;

namespace SchoolManagement.Application.Validators;

public class CreateParentRequestValidator : AbstractValidator<CreateParentRequest>
{
    public CreateParentRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Mobile).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrEmpty(x.Email));
    }
}
