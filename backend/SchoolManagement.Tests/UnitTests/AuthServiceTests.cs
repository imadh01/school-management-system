using Moq;
using SchoolManagement.Application.DTOs.Auth;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Application.Services;
using SchoolManagement.Domain.Entities;
using Xunit;

namespace SchoolManagement.Tests.UnitTests;

public class AuthServiceTests
{
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly Mock<IJwtTokenGenerator> _tokenGenerator = new();
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _sut = new AuthService(_userRepository.Object, _passwordHasher.Object, _tokenGenerator.Object);
    }

    private static User ActiveUser() => new()
    {
        Id = 1,
        Username = "admin",
        Email = "admin@test.com",
        PasswordHash = "hashed-value",
        Status = "Active",
        UserRoles = new List<UserRole>
        {
            new() { Role = new Role { Name = "Admin" } }
        }
    };

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsAuthResult()
    {
        var user = ActiveUser();
        _userRepository.Setup(r => r.GetByUsernameOrEmailAsync("admin", default)).ReturnsAsync(user);
        _userRepository.Setup(r => r.GetPermissionNamesAsync(1, default))
            .ReturnsAsync((IReadOnlyList<string>)new List<string> { "Users.Create" });
        _passwordHasher.Setup(h => h.VerifyPassword("hashed-value", "Admin@123")).Returns(true);
        _tokenGenerator
            .Setup(t => t.GenerateToken(
                user,
                It.Is<IReadOnlyList<string>>(r => r.Contains("Admin")),
                It.Is<IReadOnlyList<string>>(p => p.Contains("Users.Create"))))
            .Returns(("fake-jwt", DateTime.UtcNow.AddHours(1)));

        var result = await _sut.LoginAsync(new LoginRequest("admin", "Admin@123"), default);

        Assert.NotNull(result);
        Assert.Equal("fake-jwt", result!.Token);
        Assert.Contains("Admin", result.Roles);
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ReturnsNull()
    {
        var user = ActiveUser();
        _userRepository.Setup(r => r.GetByUsernameOrEmailAsync("admin", default)).ReturnsAsync(user);
        _passwordHasher.Setup(h => h.VerifyPassword(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        var result = await _sut.LoginAsync(new LoginRequest("admin", "WrongPassword1!"), default);

        Assert.Null(result);
    }

    [Fact]
    public async Task LoginAsync_UserDoesNotExist_ReturnsNull()
    {
        _userRepository.Setup(r => r.GetByUsernameOrEmailAsync("nobody", default)).ReturnsAsync((User?)null);

        var result = await _sut.LoginAsync(new LoginRequest("nobody", "whatever"), default);

        Assert.Null(result);
    }

    [Fact]
    public async Task LoginAsync_InactiveUser_ReturnsNull()
    {
        var user = ActiveUser();
        user.Status = "Suspended";
        _userRepository.Setup(r => r.GetByUsernameOrEmailAsync("admin", default)).ReturnsAsync(user);

        var result = await _sut.LoginAsync(new LoginRequest("admin", "Admin@123"), default);

        Assert.Null(result);
        _passwordHasher.Verify(h => h.VerifyPassword(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }
}