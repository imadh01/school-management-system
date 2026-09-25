using FluentValidation;
using SchoolManagement.Application.DTOs.Auth;

namespace SchoolManagement.Application.Validators;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.UsernameOrEmail)
            .NotEmpty();

        // Intentionally NOT re-checking password complexity here — that
        // rule applies at account creation, not at every login attempt.
        RuleFor(x => x.Password)
            .NotEmpty();
    }
}