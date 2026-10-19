using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.Validators;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Tests.Support;
using Xunit;

namespace SchoolManagement.Tests.DatabaseRules;

/// <summary>
/// Fix 5. The text limits the parent validators enforce (ParentFieldLimits) must equal the widths of the
/// Parents columns in the EF model, so a change on either side that forgets the other is a failing test.
/// </summary>
[Collection("Database")]
public class ParentFieldLimitsTests
{
    private readonly SqlServerWebApplicationFactory _factory;

    public ParentFieldLimitsTests(SqlServerWebApplicationFactory factory) => _factory = factory;

    [Theory]
    [InlineData(nameof(Parent.Name), ParentFieldLimits.Name)]
    [InlineData(nameof(Parent.Email), ParentFieldLimits.Email)]
    [InlineData(nameof(Parent.Mobile), ParentFieldLimits.Phone)]
    [InlineData(nameof(Parent.Occupation), ParentFieldLimits.Occupation)]
    [InlineData(nameof(Parent.Nationality), ParentFieldLimits.Nationality)]
    [InlineData(nameof(Parent.CountryOfResidence), ParentFieldLimits.CountryOfResidence)]
    [InlineData(nameof(Parent.Timezone), ParentFieldLimits.Timezone)]
    [InlineData(nameof(Parent.PreferredLanguage), ParentFieldLimits.PreferredLanguage)]
    [InlineData(nameof(Parent.PreferredContactMethod), ParentFieldLimits.PreferredContactMethod)]
    [InlineData(nameof(Parent.Whatsapp), ParentFieldLimits.Phone)]
    [InlineData(nameof(Parent.Employer), ParentFieldLimits.Employer)]
    [InlineData(nameof(Parent.JobTitle), ParentFieldLimits.JobTitle)]
    [InlineData(nameof(Parent.WorkEmail), ParentFieldLimits.Email)]
    [InlineData(nameof(Parent.WorkPhone), ParentFieldLimits.Phone)]
    public void The_validator_limit_equals_the_column_width(string property, int validatorLimit)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var columnWidth = db.Model.FindEntityType(typeof(Parent))!.FindProperty(property)!.GetMaxLength();

        Assert.Equal(columnWidth, validatorLimit);
    }
}
