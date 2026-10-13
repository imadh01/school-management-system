using SchoolManagement.Application.DTOs.Addresses;
using SchoolManagement.Application.Validators;
using SchoolManagement.Domain.Entities;
using Xunit;

namespace SchoolManagement.Tests.UnitTests;

/// <summary>G1a: the address rules the forms get back before anything reaches the database.</summary>
public class AddressValidationTests
{
    private static AddressRequest Address(string type, bool primary) =>
        new(type, primary, "Line one", null, "City", CountryId: 1, RegionId: 1, PostalCode: null);

    [Fact]
    public void One_address_of_each_type_is_valid()
    {
        var list = new[] { Address("Home", true), Address("Permanent", false), Address("Mailing", false) };
        Assert.True(AddressListRules.OnePerType(list));
        Assert.True(AddressListRules.ExactlyOnePrimary(list));
    }

    [Fact]
    public void Two_addresses_of_the_same_type_are_refused()
    {
        Assert.False(AddressListRules.OnePerType(new[] { Address("Home", true), Address("Home", false) }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void A_list_needs_exactly_one_primary(int primaries)
    {
        var list = new[] { Address("Home", primaries >= 1), Address("Mailing", primaries >= 2) };
        Assert.False(AddressListRules.ExactlyOnePrimary(list));
    }

    [Fact]
    public void No_addresses_at_all_is_valid()
    {
        Assert.True(AddressListRules.ExactlyOnePrimary(Array.Empty<AddressRequest>()));
        Assert.True(AddressListRules.ExactlyOnePrimary(null));
    }

    [Theory]
    [InlineData("Office")]
    [InlineData("home")]
    public void An_unknown_address_type_is_refused(string type)
    {
        var result = new AddressRequestValidator().Validate(Address(type, true));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(AddressRequest.AddressType));
    }

    [Fact]
    public void Line1_city_and_country_are_required()
    {
        var result = new AddressRequestValidator().Validate(new AddressRequest("Home", true, "", null, " ", 0, null, null));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(AddressRequest.Line1));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(AddressRequest.City));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(AddressRequest.CountryId));
    }

    [Fact]
    public void The_type_list_is_the_one_the_database_checks()
    {
        Assert.Equal(new[] { "Home", "Permanent", "Mailing" }, AddressTypes.All);
    }
}
