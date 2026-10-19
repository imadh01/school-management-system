using SchoolManagement.Application.Services;
using Xunit;

namespace SchoolManagement.Tests.UnitTests;

/// <summary>Emirates ID: optional, 15 digits starting with 784; dashes and spaces are allowed when typing.</summary>
public class EmiratesIdRulesTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("784199012345671")]
    [InlineData("784-1990-1234567-1")]
    [InlineData(" 784 1990 1234567 1 ")]
    public void Valid_or_blank_values_are_accepted(string? value) => Assert.True(EmiratesIdRules.IsValid(value));

    [Theory]
    [InlineData("78419901234567")]     // 14 digits
    [InlineData("7841990123456711")]   // 16 digits
    [InlineData("123199012345671")]    // does not start with 784
    [InlineData("78419901234567A")]    // a letter
    [InlineData("784-1990-1234567-")]  // 14 digits after removing dashes
    public void Wrong_values_are_refused(string value) => Assert.False(EmiratesIdRules.IsValid(value));

    [Fact]
    public void Normalize_keeps_digits_only_and_blank_becomes_null()
    {
        Assert.Equal("784199012345671", EmiratesIdRules.Normalize("784-1990-1234567-1"));
        Assert.Null(EmiratesIdRules.Normalize("  "));
        Assert.Null(EmiratesIdRules.Normalize(null));
    }

    [Fact]
    public void Format_adds_the_dashes_and_leaves_anything_else_alone()
    {
        Assert.Equal("784-1990-1234567-1", EmiratesIdRules.Format("784199012345671"));
        Assert.Equal("12345", EmiratesIdRules.Format("12345"));
        Assert.Null(EmiratesIdRules.Format(null));
    }
}
