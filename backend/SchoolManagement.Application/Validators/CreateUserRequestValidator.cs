using FluentValidation;
using SchoolManagement.Application.DTOs.Users;

namespace SchoolManagement.Application.Validators;

public class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.Username).MustBeValidUsername();
        RuleFor(x => x.Email).NotEmpty().MaximumLength(100).EmailAddress();
        RuleFor(x => x.RoleName).NotEmpty();
        RuleFor(x => x.Password).NotEmpty().MustBeStrongPassword();
        RuleFor(x => x.Profile).NotNull().SetValidator(new StaffProfileRequestValidator());
        this.ValidateAddresses(x => x.Addresses);
        RuleFor(x => x.Specialization).MaximumLength(100);
    }
}
