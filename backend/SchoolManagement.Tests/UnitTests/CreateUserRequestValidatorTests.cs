// E2: the request rules for the Users form, My Account and Settings (no database).

using SchoolManagement.Application.DTOs.Settings;
using SchoolManagement.Application.DTOs.Users;
using SchoolManagement.Application.Validators;
using SchoolManagement.Tests.Support;
using Xunit;

namespace SchoolManagement.Tests.UnitTests;

public class CreateUserRequestValidatorTests
{
    private readonly CreateUserRequestValidator _create = new();
    private readonly UpdateSchoolSettingsRequestValidator _settings = new();

    private static CreateUserRequest Valid() =>
        new("new.user", "new@test.com", "Passw0rd!", "Clerk", TestData.StaffProfile());

    [Fact]
    public void A_complete_request_is_valid() => Assert.True(_create.Validate(Valid()).IsValid);

    [Fact]
    public void The_four_required_profile_fields_are_reported_by_name()
    {
        var request = Valid() with
        {
            Profile = TestData.StaffProfile() with { FullName = "", Gender = null, Mobile = null, JoiningDate = null },
        };

        var errors = _create.Validate(request).Errors.Select(e => e.PropertyName).ToHashSet();

        Assert.Contains("Profile.FullName", errors);
        Assert.Contains("Profile.Gender", errors);
        Assert.Contains("Profile.Mobile", errors);
        Assert.Contains("Profile.JoiningDate", errors);
    }

    [Theory]
    [InlineData("Other")]
    [InlineData("male")]
    public void Gender_must_be_Male_or_Female(string gender)
    {
        var request = Valid() with { Profile = TestData.StaffProfile() with { Gender = gender } };

        Assert.Contains(_create.Validate(request).Errors, e => e.PropertyName == "Profile.Gender");
    }

    [Fact]
    public void A_weak_password_is_refused()
    {
        Assert.Contains(_create.Validate(Valid() with { Password = "password" }).Errors, e => e.PropertyName == "Password");
    }

    [Fact]
    public void A_date_of_birth_in_the_future_is_refused()
    {
        var request = Valid() with
        {
            Profile = TestData.StaffProfile() with { DateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1) },
        };

        Assert.Contains(_create.Validate(request).Errors, e => e.PropertyName == "Profile.DateOfBirth");
    }

    // ── Settings ──────────────────────────────────────────────────────

    private static UpdateSchoolSettingsRequest ValidSettings() => new(
        "Test School", "TS", "office@test.school", "+971 4 123 4567", "https://test.school", null, null,
        "Asia/Dubai", "AED", new List<string> { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday" },
        null, "AAAAAAAAB9E=");

    [Fact]
    public void Valid_settings_pass() => Assert.True(_settings.Validate(ValidSettings()).IsValid);

    [Theory]
    [InlineData("Arabian Standard Time")] // a Windows id: refused so the value never depends on the server OS
    [InlineData("Asia/Atlantis")]
    [InlineData("")]
    public void Time_zone_must_be_a_known_IANA_id(string id)
    {
        Assert.Contains(_settings.Validate(ValidSettings() with { TimeZoneId = id }).Errors, e => e.PropertyName == "TimeZoneId");
    }

    [Theory]
    [InlineData("aed")]
    [InlineData("AE")]
    [InlineData("AED1")]
    public void Currency_must_be_three_capital_letters(string code)
    {
        Assert.Contains(_settings.Validate(ValidSettings() with { CurrencyCode = code }).Errors, e => e.PropertyName == "CurrencyCode");
    }

    [Fact]
    public void At_least_one_known_working_day_and_no_repeats()
    {
        Assert.False(_settings.Validate(ValidSettings() with { WorkingDays = new() }).IsValid);
        Assert.False(_settings.Validate(ValidSettings() with { WorkingDays = new() { "Funday" } }).IsValid);
        Assert.False(_settings.Validate(ValidSettings() with { WorkingDays = new() { "Monday", "Monday" } }).IsValid);
    }

    [Fact]
    public void Website_must_be_a_full_http_address()
    {
        Assert.False(_settings.Validate(ValidSettings() with { Website = "test.school" }).IsValid);
        Assert.False(_settings.Validate(ValidSettings() with { Website = "ftp://test.school" }).IsValid);
    }
}
