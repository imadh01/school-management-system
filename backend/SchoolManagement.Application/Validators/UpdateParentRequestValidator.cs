using FluentValidation;
using SchoolManagement.Application.DTOs.Parents;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Application.Validators;

public class UpdateParentRequestValidator : AbstractValidator<UpdateParentRequest>
{
    public UpdateParentRequestValidator()
    {
        RuleFor(x => x.RowVersion).Must(RowVersionRules.IsValid).WithMessage(RowVersionRules.Message);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Mobile).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrEmpty(x.Email));
        RuleFor(x => x.Status).Must(ActiveStatuses.IsValid)
            .WithMessage("Status must be 'Active' or 'Inactive'.");
    }
}
