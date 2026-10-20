using SchoolManagement.Application.DTOs.AcademicYears;
using SchoolManagement.Application.Validators;
using Xunit;

namespace SchoolManagement.Tests.UnitTests;

/// <summary>The Academic Years request validators: no database, no web host.</summary>
public class AcademicYearValidatorsTests
{
    private static readonly DateOnly Start = new(2030, 6, 1);
    private static readonly DateOnly End = new(2031, 3, 31);
    private const string Version = "AAAAAAAAB9E="; // 8 bytes in base64

    [Fact]
    public void A_valid_create_request_passes() =>
        Assert.True(new CreateAcademicYearRequestValidator()
            .Validate(new CreateAcademicYearRequest("2030-2031", Start, End)).IsValid);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("123456789012345678901")] // 21 characters, the column holds 20
    public void The_name_is_required_and_limited_to_20_characters(string name) =>
        Assert.False(new CreateAcademicYearRequestValidator()
            .Validate(new CreateAcademicYearRequest(name, Start, End)).IsValid);

    [Fact]
    public void The_start_date_must_be_before_the_end_date()
    {
        var validator = new CreateAcademicYearRequestValidator();
        Assert.False(validator.Validate(new CreateAcademicYearRequest("X", End, Start)).IsValid);
        Assert.False(validator.Validate(new CreateAcademicYearRequest("X", Start, Start)).IsValid);
    }

    [Fact]
    public void Missing_dates_are_refused() =>
        Assert.False(new CreateAcademicYearRequestValidator()
            .Validate(new CreateAcademicYearRequest("X", default, default)).IsValid);

    [Fact]
    public void An_update_needs_a_row_version_and_the_same_rules()
    {
        var validator = new UpdateAcademicYearRequestValidator();
        Assert.True(validator.Validate(new UpdateAcademicYearRequest("X", Start, End, Version)).IsValid);
        Assert.False(validator.Validate(new UpdateAcademicYearRequest("X", Start, End, "")).IsValid);
        Assert.False(validator.Validate(new UpdateAcademicYearRequest("X", Start, End, "not-base64!")).IsValid);
        Assert.False(validator.Validate(new UpdateAcademicYearRequest("X", End, Start, Version)).IsValid);
    }

    [Fact]
    public void Set_current_needs_a_row_version() =>
        Assert.False(new SetCurrentAcademicYearRequestValidator()
            .Validate(new SetCurrentAcademicYearRequest("", null)).IsValid);

    [Fact]
    public void Close_needs_a_row_version_and_the_typed_name()
    {
        var validator = new CloseAcademicYearRequestValidator();
        Assert.True(validator.Validate(new CloseAcademicYearRequest(Version, "2030-2031")).IsValid);
        Assert.False(validator.Validate(new CloseAcademicYearRequest(Version, "")).IsValid);
        Assert.False(validator.Validate(new CloseAcademicYearRequest("", "2030-2031")).IsValid);
    }
}
