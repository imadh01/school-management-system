using FluentValidation.TestHelper;
using SchoolManagement.Application.DTOs.Auth;
using SchoolManagement.Application.Validators;
using Xunit;

namespace SchoolManagement.Tests.UnitTests;

public class LoginRequestValidatorTests
{
    private readonly LoginRequestValidator _sut = new();

    [Fact]
    public void ValidRequest_PassesValidation()
    {
        var result = _sut.TestValidate(new LoginRequest("admin", "whatever-they-typed"));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyUsernameOrEmail_FailsValidation()
    {
        var result = _sut.TestValidate(new LoginRequest("", "Passw0rd!"));
        result.ShouldHaveValidationErrorFor(x => x.UsernameOrEmail);
    }

    [Fact]
    public void EmptyPassword_FailsValidation()
    {
        var result = _sut.TestValidate(new LoginRequest("admin", ""));
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void WeakPassword_StillPassesValidation()
    {
        // Deliberately proving the earlier design decision: login must
        // NOT enforce the creation-time complexity policy, or an old
        // password from before the policy existed would lock users out.
        var result = _sut.TestValidate(new LoginRequest("admin", "weak"));
        result.ShouldNotHaveValidationErrorFor(x => x.Password);
    }
}