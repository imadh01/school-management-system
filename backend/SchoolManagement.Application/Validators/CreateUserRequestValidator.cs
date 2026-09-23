using FluentValidation;
using SchoolManagement.Application.DTOs.Users;

namespace SchoolManagement.Application.Validators;

public class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty()
            .MaximumLength(50)
            .Matches("^[a-zA-Z0-9._-]+$")
                .WithMessage("Username can only contain letters, digits, dots, underscores, and hyphens.");

        RuleFor(x => x.Email)
            .NotEmpty()
            .MaximumLength(100)
            .EmailAddress();

        RuleFor(x => x.RoleName)
            .NotEmpty();

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8)
                .WithMessage("Password must be at least 8 characters long.")
            .Matches("[A-Z]")
                .WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[a-z]")
                .WithMessage("Password must contain at least one lowercase letter.")
            .Matches("[0-9]")
                .WithMessage("Password must contain at least one digit.")
            .Matches("[^a-zA-Z0-9]")
                .WithMessage("Password must contain at least one special character.");
    }
}
