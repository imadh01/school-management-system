// E2: UserService in isolation (repositories, account service and address service are mocked).
// The HTTP + database behaviour (roles, teacher record, last Admin, sessions) is in UsersApiTests.

using Moq;
using SchoolManagement.Application.DTOs.Addresses;
using SchoolManagement.Application.DTOs.Users;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Application.Services;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Exceptions;
using SchoolManagement.Tests.Support;
using Xunit;

namespace SchoolManagement.Tests.UnitTests;

public class UserServiceTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<ITeacherRepository> _teachers = new();
    private readonly Mock<IUserAccountService> _accounts = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly Mock<IAddressService> _addresses = new();
    private readonly Mock<IConcurrencyGuard> _concurrency = new();
    private readonly Mock<IPermissionCacheService> _cache = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly UserService _sut;

    public UserServiceTests()
    {
        _users.Setup(r => r.ExecuteInTransactionAsync(It.IsAny<Func<Task<bool>>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<Task<bool>> operation, CancellationToken _) => operation());
        _addresses.Setup(a => a.BuildAsync(It.IsAny<IReadOnlyList<AddressRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Address>());
        _accounts.Setup(a => a.PrepareRoleAsync("Clerk", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Role { Id = 5, Name = "Clerk", IsActive = true });
        _passwordHasher.Setup(h => h.HashPassword("Passw0rd!")).Returns("hashed-output");

        _sut = new UserService(_users.Object, _teachers.Object, _accounts.Object, _passwordHasher.Object,
            _addresses.Object, _concurrency.Object, _cache.Object, _currentUser.Object);
    }

    private static CreateUserRequest ValidRequest(string? employeeNumber = null) =>
        new("newuser", "new@test.com", "Passw0rd!", "Clerk", TestData.StaffProfile("New User", employeeNumber));

    [Fact]
    public async Task Create_hashes_the_password_forces_a_change_and_saves_the_profile_once()
    {
        var result = await _sut.CreateUserAsync(ValidRequest("EMP-1"), default);

        Assert.Equal("newuser", result.Username);
        Assert.True(result.MustChangePassword);
        Assert.Equal("New User", result.Profile!.FullName);
        Assert.False(result.Profile.NeedsName);
        _users.Verify(r => r.AddAsync(
            It.Is<User>(u => u.PasswordHash == "hashed-output"
                             && u.MustChangePassword
                             && u.Status == UserStatuses.Active
                             && u.StaffProfile!.EmployeeNumber == "EMP-1"),
            It.IsAny<CancellationToken>()), Times.Once);
        // The role (and, for teachers, the teacher record) is given by the account service, inside the transaction.
        _accounts.Verify(a => a.ApplyRoleChangeAsync(It.IsAny<User>(), It.Is<Role>(r => r.Name == "Clerk"),
            null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_with_a_taken_username_is_a_conflict_and_saves_nothing()
    {
        _users.Setup(r => r.ExistsByUsernameAsync("newuser", null, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await Assert.ThrowsAsync<ConflictException>(() => _sut.CreateUserAsync(ValidRequest(), default));
        _users.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_with_a_taken_email_is_a_conflict_and_saves_nothing()
    {
        _users.Setup(r => r.ExistsByEmailAsync("new@test.com", null, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await Assert.ThrowsAsync<ConflictException>(() => _sut.CreateUserAsync(ValidRequest(), default));
        _users.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_with_a_taken_employee_number_is_a_conflict_and_saves_nothing()
    {
        _users.Setup(r => r.ExistsByEmployeeNumberAsync("EMP-1", null, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await Assert.ThrowsAsync<ConflictException>(() => _sut.CreateUserAsync(ValidRequest("EMP-1"), default));
        _users.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_without_an_employee_number_does_not_look_one_up()
    {
        await _sut.CreateUserAsync(ValidRequest(employeeNumber: "  "), default);

        _users.Verify(r => r.ExistsByEmployeeNumberAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Create_with_a_refused_role_saves_nothing()
    {
        _accounts.Setup(a => a.PrepareRoleAsync("Parent", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BusinessRuleException("not yet"));
        var request = ValidRequest() with { RoleName = "Parent" };

        await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.CreateUserAsync(request, default));
        _users.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task My_profile_without_a_signed_in_user_is_refused()
    {
        _currentUser.Setup(u => u.UserId).Returns((int?)null);

        await Assert.ThrowsAsync<ForbiddenException>(() => _sut.GetMyProfileAsync(default));
    }

    [Fact]
    public async Task My_profile_of_an_account_without_a_staff_profile_is_not_found()
    {
        _currentUser.Setup(u => u.UserId).Returns(7);
        _users.Setup(r => r.GetWithProfileAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 7, Username = "student1" });

        await Assert.ThrowsAsync<NotFoundException>(() => _sut.GetMyProfileAsync(default));
    }
}
