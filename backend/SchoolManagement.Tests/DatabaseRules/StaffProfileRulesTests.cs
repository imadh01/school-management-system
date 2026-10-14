using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Domain.Constants;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Migrations;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Tests.Support;
using Xunit;

namespace SchoolManagement.Tests.DatabaseRules;

/// <summary>
/// E2a: the staff-profile and school-settings rules hold in the DATABASE, whatever code writes the rows:
/// one profile per user, employee numbers unique when present, staff addresses as a fourth owner, a single
/// settings row with sane values; and the migration's profile copy does what decision 2/3 say.
/// </summary>
[Collection("Database")]
public class StaffProfileRulesTests
{
    private readonly SqlServerWebApplicationFactory _factory;

    public StaffProfileRulesTests(SqlServerWebApplicationFactory factory) => _factory = factory;

    // ------------------------------------------------------------------ staff profiles

    [Fact]
    public async Task A_user_has_at_most_one_profile()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await NewUserAsync(db);

        db.StaffProfiles.Add(new StaffProfile { UserId = user.Id, FullName = "First" });
        await db.SaveChangesAsync();
        // Forget the first one, or EF itself "moves" the user to the new profile before SQL Server sees it.
        db.ChangeTracker.Clear();
        db.StaffProfiles.Add(new StaffProfile { UserId = user.Id, FullName = "Second" });

        await AssertViolatesAsync(db, "UX_StaffProfiles_UserId");
    }

    [Fact]
    public async Task Employee_numbers_are_unique_but_may_be_left_empty()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var number = "EMP-" + TestData.Unique();

        // Two profiles without a number: fine (filtered index, decision 4).
        db.StaffProfiles.Add(new StaffProfile { UserId = (await NewUserAsync(db)).Id, FullName = "No number 1" });
        db.StaffProfiles.Add(new StaffProfile { UserId = (await NewUserAsync(db)).Id, FullName = "No number 2" });
        db.StaffProfiles.Add(new StaffProfile { UserId = (await NewUserAsync(db)).Id, FullName = "Has number", EmployeeNumber = number });
        await db.SaveChangesAsync();

        db.StaffProfiles.Add(new StaffProfile { UserId = (await NewUserAsync(db)).Id, FullName = "Same number", EmployeeNumber = number });
        await AssertViolatesAsync(db, "UX_StaffProfiles_EmployeeNumber");
    }

    [Theory]
    [InlineData("Other", null, "CK_StaffProfiles_Gender")]
    [InlineData("Male", "   ", "CK_StaffProfiles_FullNameNotBlank")]
    public async Task Gender_and_name_are_checked(string gender, string? fullName, string constraint)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await NewUserAsync(db);

        db.StaffProfiles.Add(new StaffProfile { UserId = user.Id, FullName = fullName ?? "Valid Name", Gender = gender });
        await AssertViolatesAsync(db, constraint);
    }

    [Fact]
    public async Task A_staff_address_has_the_profile_as_its_only_owner()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var uae = await db.Countries.Include(c => c.Regions).SingleAsync(c => c.Code == "AE");
        var profile = new StaffProfile { UserId = (await NewUserAsync(db)).Id, FullName = "With address" };
        profile.Addresses.Add(NewAddress(uae));
        db.StaffProfiles.Add(profile);
        await db.SaveChangesAsync(); // one owner: fine

        var parent = new Parent { Name = "Parent " + TestData.Unique(), Mobile = "8" + Random.Shared.NextInt64(100_000_000, 999_999_999) };
        db.Parents.Add(parent);
        await db.SaveChangesAsync();
        var twoOwners = NewAddress(uae);
        twoOwners.StaffProfileId = profile.Id;
        twoOwners.ParentId = parent.Id;
        twoOwners.AddressType = AddressTypes.Mailing;
        twoOwners.IsPrimary = false; // so only the owner CHECK can object
        db.Addresses.Add(twoOwners);

        await AssertViolatesAsync(db, "CK_Addresses_ExactlyOneOwner");
    }

    // ------------------------------------------------------------------ school settings

    [Fact]
    public async Task The_migration_seeds_exactly_one_settings_row()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var rows = await db.SchoolSettings.AsNoTracking().ToListAsync();

        var row = Assert.Single(rows);
        Assert.Equal(SchoolSettings.SingletonId, row.Id);
        Assert.False(string.IsNullOrWhiteSpace(row.TimeZoneId));
        Assert.Matches("^[A-Z]{3}$", row.CurrencyCode);
    }

    [Fact]
    public async Task A_second_settings_row_is_rejected()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        db.SchoolSettings.Add(new SchoolSettings
        {
            Id = 2, SchoolName = "Second school", TimeZoneId = "Asia/Dubai", CurrencyCode = "AED",
            WorkingDays = WorkingDays.Monday,
        });

        await AssertViolatesAsync(db, "CK_SchoolSettings_SingleRow");
    }

    [Theory]
    [InlineData("aed", WorkingDays.Monday, "CK_SchoolSettings_CurrencyCode")]
    [InlineData("A1D", WorkingDays.Monday, "CK_SchoolSettings_CurrencyCode")]
    [InlineData("AED", (byte)0, "CK_SchoolSettings_WorkingDays")]
    [InlineData("AED", (byte)128, "CK_SchoolSettings_WorkingDays")]
    public async Task Currency_and_working_days_are_checked(string currency, byte workingDays, string constraint)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var settings = await db.SchoolSettings.SingleAsync();

        settings.CurrencyCode = currency;
        settings.WorkingDays = workingDays;

        await AssertViolatesAsync(db, constraint);
    }

    [Fact]
    public void Working_days_round_trip_between_days_and_the_bit_mask()
    {
        var days = new[] { DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Thursday };

        var mask = WorkingDays.FromDays(days);

        Assert.Equal(WorkingDays.Sunday | WorkingDays.Monday | WorkingDays.Thursday, mask);
        Assert.Equal(new[] { DayOfWeek.Monday, DayOfWeek.Thursday, DayOfWeek.Sunday }, WorkingDays.ToDays(mask)); // Monday first
    }

    // ------------------------------------------------------------------ the migration's copy

    [Fact]
    public async Task The_copy_gives_teachers_their_name_and_phone_and_other_staff_their_username()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var clerk = await NewUserAsync(db, RoleNames.Clerk);
        var teacherUser = await NewUserAsync(db, RoleNames.Teacher);
        db.Teachers.Add(new Teacher { UserId = teacherUser.Id, Name = "  Asha Rao ", Phone = "+971 50 000 0000" });
        var parentUser = await NewUserAsync(db, RoleNames.Parent);
        var deleted = await NewUserAsync(db, RoleNames.Clerk);
        deleted.IsDeleted = true;
        deleted.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        await db.Database.ExecuteSqlRawAsync(E2a_StaffProfilesAndSchoolSettings.CopyProfilesSql);
        await db.Database.ExecuteSqlRawAsync(E2a_StaffProfilesAndSchoolSettings.CopyProfilesSql); // runs twice: no duplicates
        await db.Database.ExecuteSqlRawAsync(E2a_StaffProfilesAndSchoolSettings.GuardTeachersHaveProfilesSql);

        var profiles = await db.StaffProfiles.AsNoTracking().IgnoreQueryFilters()
            .Where(p => new[] { clerk.Id, teacherUser.Id, parentUser.Id, deleted.Id }.Contains(p.UserId))
            .ToListAsync();

        var clerkProfile = Assert.Single(profiles, p => p.UserId == clerk.Id);
        Assert.Equal(clerk.Username, clerkProfile.FullName); // decision 3: flagged "needs name" in the list
        Assert.Null(clerkProfile.Mobile);
        Assert.Equal(DateOnly.FromDateTime(clerk.CreatedAt), clerkProfile.JoiningDate);

        var teacherProfile = Assert.Single(profiles, p => p.UserId == teacherUser.Id);
        Assert.Equal("Asha Rao", teacherProfile.FullName);
        Assert.Equal("+971 50 000 0000", teacherProfile.Mobile);

        Assert.DoesNotContain(profiles, p => p.UserId == parentUser.Id); // decision 1: no parent/student profiles
        Assert.DoesNotContain(profiles, p => p.UserId == deleted.Id);
    }

    [Fact]
    public async Task The_guard_stops_when_a_teacher_has_no_profile()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var teacherUser = await NewUserAsync(db, RoleNames.Teacher);
        var teacher = new Teacher { UserId = teacherUser.Id, Name = "No profile" };
        db.Teachers.Add(teacher);
        await db.SaveChangesAsync();

        try
        {
            var ex = await Assert.ThrowsAsync<SqlException>(
                () => db.Database.ExecuteSqlRawAsync(E2a_StaffProfilesAndSchoolSettings.GuardTeachersHaveProfilesSql));
            Assert.Equal(50002, ex.Number);
        }
        finally
        {
            // Leave the shared database as the copy would: other tests run the guard too.
            db.StaffProfiles.Add(new StaffProfile { UserId = teacherUser.Id, FullName = teacher.Name });
            await db.SaveChangesAsync();
        }
    }

    // ------------------------------------------------------------------ helpers

    private static async Task<User> NewUserAsync(ApplicationDbContext db, string? roleName = null)
    {
        var name = "sp_" + TestData.Unique();
        var user = new User { Username = name, Email = $"{name}@test.local", PasswordHash = "x", Status = "Active" };
        if (roleName is not null)
        {
            var role = await db.Roles.SingleAsync(r => r.Name == roleName);
            user.UserRoles.Add(new UserRole { RoleId = role.Id, AssignedAt = DateTime.UtcNow });
        }
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static Address NewAddress(Country country) => new()
    {
        AddressType = AddressTypes.Home,
        IsPrimary = true,
        Line1 = "Villa 1",
        City = "Abu Dhabi",
        CountryId = country.Id,
        RegionId = country.Regions.First().Id,
    };

    /// <summary>The save fails, and the database names the rule that stopped it.</summary>
    private static async Task AssertViolatesAsync(ApplicationDbContext db, string constraintOrIndex)
    {
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        var sql = Assert.IsType<SqlException>(ex.InnerException);
        Assert.Contains(constraintOrIndex, sql.Message);
    }
}
