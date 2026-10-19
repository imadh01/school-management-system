using SchoolManagement.Application.DTOs.Parents;
using SchoolManagement.Application.Validators;
using Xunit;

namespace SchoolManagement.Tests.UnitTests;

/// <summary>
/// Fix 5. The parent create and edit requests are checked against the lengths of the database columns and
/// the formats of the email and phone fields, with the error named after the field. Before the fix only
/// Name, Mobile (length), Email (format) and Status were checked, so a longer value reached the database and
/// came back as a 500.
/// </summary>
public class ParentRequestValidatorTests
{
    private static readonly string ValidVersion = Convert.ToBase64String(new byte[8]);

    private static CreateParentRequest ValidCreate() => new(
        Name: "Mary Thomas", Email: "mary@example.com", Mobile: "+971 50 123 4567",
        Occupation: null, Nationality: null, CountryOfResidence: null, Timezone: null,
        PreferredLanguage: null, PreferredContactMethod: null, Whatsapp: null, EmergencyOnly: false,
        NotifyAttendance: true, NotifyExams: true, NotifyFees: true, NotifyNotices: true, NotifyDiscipline: true,
        Employer: null, JobTitle: null, WorkEmail: null, WorkPhone: null, BillingContact: false,
        Addresses: null);

    private static UpdateParentRequest ValidUpdate() => new(
        Name: "Mary Thomas", Email: "mary@example.com", Mobile: "+971 50 123 4567", Status: "Active",
        Occupation: null, Nationality: null, CountryOfResidence: null, Timezone: null,
        PreferredLanguage: null, PreferredContactMethod: null, Whatsapp: null, EmergencyOnly: false,
        NotifyAttendance: true, NotifyExams: true, NotifyFees: true, NotifyNotices: true, NotifyDiscipline: true,
        Employer: null, JobTitle: null, WorkEmail: null, WorkPhone: null, BillingContact: false,
        Addresses: null, RowVersion: ValidVersion);

    /// <summary>Property name and the column width it must not exceed (Parents table).</summary>
    public static IEnumerable<object[]> TextLimits() => new[]
    {
        new object[] { "Name", 100 },
        new object[] { "Email", 100 },
        new object[] { "Occupation", 100 },
        new object[] { "Nationality", 50 },
        new object[] { "CountryOfResidence", 50 },
        new object[] { "Timezone", 50 },
        new object[] { "PreferredLanguage", 30 },
        new object[] { "PreferredContactMethod", 30 },
        new object[] { "Employer", 100 },
        new object[] { "JobTitle", 100 },
        new object[] { "WorkEmail", 100 },
    };

    // ------------------------------------------------------------------ valid requests

    [Fact]
    public void A_valid_create_request_passes() =>
        Assert.True(new CreateParentRequestValidator().Validate(ValidCreate()).IsValid);

    [Fact]
    public void A_valid_update_request_passes() =>
        Assert.True(new UpdateParentRequestValidator().Validate(ValidUpdate()).IsValid);

    // ------------------------------------------------------------------ lengths

    [Theory]
    [MemberData(nameof(TextLimits))]
    public void Create_rejects_a_value_one_character_over_the_column_with_the_field_named(string property, int limit)
    {
        var result = new CreateParentRequestValidator().Validate(Set(ValidCreate(), property, Over(property, limit)));

        Assert.Contains(result.Errors, e => e.PropertyName == property);
    }

    [Theory]
    [MemberData(nameof(TextLimits))]
    public void Update_rejects_a_value_one_character_over_the_column_with_the_field_named(string property, int limit)
    {
        var result = new UpdateParentRequestValidator().Validate(Set(ValidUpdate(), property, Over(property, limit)));

        Assert.Contains(result.Errors, e => e.PropertyName == property);
    }

    [Theory]
    [MemberData(nameof(TextLimits))]
    public void A_value_exactly_at_the_column_width_is_accepted(string property, int limit)
    {
        var createResult = new CreateParentRequestValidator().Validate(Set(ValidCreate(), property, Exactly(property, limit)));
        var updateResult = new UpdateParentRequestValidator().Validate(Set(ValidUpdate(), property, Exactly(property, limit)));

        Assert.DoesNotContain(createResult.Errors, e => e.PropertyName == property);
        Assert.DoesNotContain(updateResult.Errors, e => e.PropertyName == property);
    }

    // ------------------------------------------------------------------ email formats

    [Theory]
    [InlineData("Email")]
    [InlineData("WorkEmail")]
    public void A_malformed_email_is_rejected_on_the_right_field(string property)
    {
        var create = new CreateParentRequestValidator().Validate(Set(ValidCreate(), property, "not-an-email"));
        var update = new UpdateParentRequestValidator().Validate(Set(ValidUpdate(), property, "not-an-email"));

        Assert.Contains(create.Errors, e => e.PropertyName == property);
        Assert.Contains(update.Errors, e => e.PropertyName == property);
    }

    // ------------------------------------------------------------------ phone formats and lengths

    [Theory]
    [InlineData("Mobile", "abc")]
    [InlineData("Mobile", "12")]
    [InlineData("Mobile", "++971501234567")]
    [InlineData("Whatsapp", "call me")]
    [InlineData("Whatsapp", "123")]
    [InlineData("WorkPhone", "office")]
    [InlineData("WorkPhone", "9")]
    public void A_malformed_phone_is_rejected_on_the_right_field(string property, string value)
    {
        var create = new CreateParentRequestValidator().Validate(Set(ValidCreate(), property, value));
        var update = new UpdateParentRequestValidator().Validate(Set(ValidUpdate(), property, value));

        Assert.Contains(create.Errors, e => e.PropertyName == property);
        Assert.Contains(update.Errors, e => e.PropertyName == property);
    }

    [Theory]
    [InlineData("Mobile")]
    [InlineData("Whatsapp")]
    [InlineData("WorkPhone")]
    public void A_phone_longer_than_the_column_is_rejected_even_when_trailing_spaces_hide_it(string property)
    {
        // 20 digits is allowed; 20 digits plus a trailing space is 21 characters in the column.
        var value = new string('1', 20) + " ";

        var result = new CreateParentRequestValidator().Validate(Set(ValidCreate(), property, value));

        Assert.Contains(result.Errors, e => e.PropertyName == property);
    }

    [Theory]
    [InlineData("Whatsapp")]
    [InlineData("WorkPhone")]
    public void Optional_phones_may_be_left_empty(string property)
    {
        Assert.True(new CreateParentRequestValidator().Validate(Set(ValidCreate(), property, null)).IsValid);
        Assert.True(new CreateParentRequestValidator().Validate(Set(ValidCreate(), property, "")).IsValid);
    }

    [Fact]
    public void Mobile_is_still_required()
    {
        var result = new CreateParentRequestValidator().Validate(Set(ValidCreate(), "Mobile", ""));

        Assert.Contains(result.Errors, e => e.PropertyName == "Mobile");
    }

    [Theory]
    [InlineData("+971 50 123 4567")]
    [InlineData("0501234567")]
    [InlineData("(04) 123-4567")]
    public void Common_phone_formats_are_accepted(string phone)
    {
        var request = Set(Set(Set(ValidCreate(), "Mobile", phone), "Whatsapp", phone), "WorkPhone", phone);

        Assert.True(new CreateParentRequestValidator().Validate(request).IsValid);
    }

    // ------------------------------------------------------------------ helpers

    private static string Over(string property, int limit) => Exactly(property, limit + 1);

    /// <summary>A value of that length that is otherwise valid for the field (email fields need an @ address).</summary>
    private static string Exactly(string property, int length) =>
        property is "Email" or "WorkEmail"
            ? new string('a', length - "@example.com".Length) + "@example.com"
            : new string('x', length);

    private static CreateParentRequest Set(CreateParentRequest r, string property, string? value) => property switch
    {
        "Name" => r with { Name = value! },
        "Email" => r with { Email = value },
        "Mobile" => r with { Mobile = value! },
        "Occupation" => r with { Occupation = value },
        "Nationality" => r with { Nationality = value },
        "CountryOfResidence" => r with { CountryOfResidence = value },
        "Timezone" => r with { Timezone = value },
        "PreferredLanguage" => r with { PreferredLanguage = value },
        "PreferredContactMethod" => r with { PreferredContactMethod = value },
        "Whatsapp" => r with { Whatsapp = value },
        "Employer" => r with { Employer = value },
        "JobTitle" => r with { JobTitle = value },
        "WorkEmail" => r with { WorkEmail = value },
        "WorkPhone" => r with { WorkPhone = value },
        _ => throw new ArgumentOutOfRangeException(nameof(property), property, null),
    };

    private static UpdateParentRequest Set(UpdateParentRequest r, string property, string? value) => property switch
    {
        "Name" => r with { Name = value! },
        "Email" => r with { Email = value },
        "Mobile" => r with { Mobile = value! },
        "Occupation" => r with { Occupation = value },
        "Nationality" => r with { Nationality = value },
        "CountryOfResidence" => r with { CountryOfResidence = value },
        "Timezone" => r with { Timezone = value },
        "PreferredLanguage" => r with { PreferredLanguage = value },
        "PreferredContactMethod" => r with { PreferredContactMethod = value },
        "Whatsapp" => r with { Whatsapp = value },
        "Employer" => r with { Employer = value },
        "JobTitle" => r with { JobTitle = value },
        "WorkEmail" => r with { WorkEmail = value },
        "WorkPhone" => r with { WorkPhone = value },
        _ => throw new ArgumentOutOfRangeException(nameof(property), property, null),
    };
}
