using SchoolManagement.Application.Services;
using SchoolManagement.Domain.Entities;
using Xunit;

namespace SchoolManagement.Tests.UnitTests;

/// <summary>The generated admission and roll number formats (no database needed).</summary>
public class NumberFormatsTests
{
    [Fact]
    public void Admission_number_is_year_then_four_digit_serial() =>
        Assert.Equal("ADM-2026-0007", NumberFormats.AdmissionNumber(2026, 7));

    [Theory]
    [InlineData("Class 5", "A", "5A")]
    [InlineData("Class 10", "b", "10B")]
    [InlineData("LKG", "A", "LKGA")]
    public void Roll_prefix_is_class_then_section(string name, string section, string expected) =>
        Assert.Equal(expected, NumberFormats.RollPrefix(new ClassSection { Name = name, Section = section }));

    [Fact]
    public void Roll_number_has_three_digit_serial() =>
        Assert.Equal("5A001", NumberFormats.RollNumber("5A", 1));

    [Fact]
    public void Max_serial_ignores_other_prefixes_and_non_numeric_tails()
    {
        var values = new[] { "5A001", "5A012", "5A", "5AX01", "10A099", "5A003" };
        Assert.Equal(12, NumberFormats.MaxSerial(values, "5A"));
        Assert.Equal(0, NumberFormats.MaxSerial(Array.Empty<string>(), "5A"));
    }
}
