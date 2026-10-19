using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Constants;
using SchoolManagement.Domain.Exceptions;
using SchoolManagement.Tests.Support;
using Xunit;

namespace SchoolManagement.Tests.DatabaseRules;

/// <summary>
/// Generated employee numbers (EMP-0001 ...) under simultaneous creates. The next number is read inside the
/// transaction behind a lock, so every account gets a different one and none fails with "just taken".
/// </summary>
[Collection("Database")]
public class EmployeeNumberTests
{
    private const int Accounts = 6;

    private readonly Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> _asAdmin;

    // Creating an account checks that the caller holds the new role's permissions, so the host runs as an Admin.
    public EmployeeNumberTests(SqlServerWebApplicationFactory factory)
    {
        _asAdmin = FakeCurrentUserPermissions.AsCaller(factory, isAdmin: true);
    }

    [Fact]
    public async Task Accounts_created_at_the_same_moment_get_different_numbers_and_none_fails()
    {
        var tag = TestData.Unique();
        var tasks = Enumerable.Range(0, Accounts).Select(async i =>
        {
            using var scope = _asAdmin.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<IUserService>();
            var name = $"emp{tag}{i}";
            // No employee number typed: the system generates it.
            return await users.CreateUserAsync(
                TestData.NewUserRequest(name, $"{name}@example.com", "Passw0rd!Aa1", RoleNames.Clerk),
                CancellationToken.None);
        }).ToList();

        var created = await Task.WhenAll(tasks); // any ConflictException ("just taken") fails the test here

        var numbers = created.Select(u => u.Profile!.EmployeeNumber).ToList();
        Assert.All(numbers, n => Assert.Matches(@"^EMP-\d{4,}$", n!));
        Assert.Equal(Accounts, numbers.Distinct().Count());
    }

    [Fact]
    public async Task A_typed_number_is_kept_and_a_duplicate_of_it_is_409()
    {
        var number = "EMP-T" + TestData.Unique();
        using (var scope = _asAdmin.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<IUserService>();
            var name = "typed" + TestData.Unique();
            var request = TestData.NewUserRequest(name, $"{name}@example.com", "Passw0rd!Aa1", RoleNames.Clerk)
                with { Profile = TestData.StaffProfile(name, number) };
            var created = await users.CreateUserAsync(request, CancellationToken.None);
            Assert.Equal(number, created.Profile!.EmployeeNumber);
        }

        using var again = _asAdmin.Services.CreateScope();
        var second = again.ServiceProvider.GetRequiredService<IUserService>();
        var other = "typed" + TestData.Unique();
        var dup = TestData.NewUserRequest(other, $"{other}@example.com", "Passw0rd!Aa1", RoleNames.Clerk)
            with { Profile = TestData.StaffProfile(other, number) };
        await Assert.ThrowsAsync<ConflictException>(() => second.CreateUserAsync(dup, CancellationToken.None));
    }
}
