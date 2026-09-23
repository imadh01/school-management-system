using Moq;
using SchoolManagement.Application.DTOs.Users;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Application.Services;
using SchoolManagement.Domain.Entities;
using Xunit;

namespace SchoolManagement.Tests.UnitTests;

public class UserServiceTests
{
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IRoleRepository> _roleRepository = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly UserService _sut;

    public UserServiceTests()
    {
        _sut = new UserService(_userRepository.Object, _roleRepository.Object, _passwordHasher.Object);
    }

    private CreateUserRequest ValidRequest() => new("newuser", "new@test.com", "Passw0rd!", "Teacher");

    [Fact]
    public async Task CreateUserAsync_ValidRequest_HashesPasswordAndAddsUser()
    {
        _userRepository.Setup(r => r.ExistsByUsernameAsync("newuser", default)).ReturnsAsync(false);
        _userRepository.Setup(r => r.ExistsByEmailAsync("new@test.com", default)).ReturnsAsync(false);
        _roleRepository.Setup(r => r.GetByNameAsync("Teacher", default)).ReturnsAsync(new Role { Id = 4, Name = "Teacher" });
        _passwordHasher.Setup(h => h.HashPassword("Passw0rd!")).Returns("hashed-output");

        var result = await _sut.CreateUserAsync(ValidRequest(), default);

        Assert.Equal("newuser", result.Username);
        Assert.Contains("Teacher", result.Roles);
        _userRepository.Verify(r => r.AddAsync(
            It.Is<User>(u => u.PasswordHash == "hashed-output" && u.PasswordHash != "Passw0rd!"),
            default), Times.Once);
    }

    [Fact]
    public async Task CreateUserAsync_DuplicateUsername_ThrowsAndDoesNotAddUser()
    {
        _userRepository.Setup(r => r.ExistsByUsernameAsync("newuser", default)).ReturnsAsync(true);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.CreateUserAsync(ValidRequest(), default));
        _userRepository.Verify(r => r.AddAsync(It.IsAny<User>(), default), Times.Never);
    }

    [Fact]
    public async Task CreateUserAsync_DuplicateEmail_ThrowsAndDoesNotAddUser()
    {
        _userRepository.Setup(r => r.ExistsByUsernameAsync("newuser", default)).ReturnsAsync(false);
        _userRepository.Setup(r => r.ExistsByEmailAsync("new@test.com", default)).ReturnsAsync(true);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.CreateUserAsync(ValidRequest(), default));
        _userRepository.Verify(r => r.AddAsync(It.IsAny<User>(), default), Times.Never);
    }

    [Fact]
    public async Task CreateUserAsync_UnknownRole_ThrowsAndDoesNotAddUser()
    {
        _userRepository.Setup(r => r.ExistsByUsernameAsync("newuser", default)).ReturnsAsync(false);
        _userRepository.Setup(r => r.ExistsByEmailAsync("new@test.com", default)).ReturnsAsync(false);
        _roleRepository.Setup(r => r.GetByNameAsync("Teacher", default)).ReturnsAsync((Role?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.CreateUserAsync(ValidRequest(), default));
        _userRepository.Verify(r => r.AddAsync(It.IsAny<User>(), default), Times.Never);
    }
}