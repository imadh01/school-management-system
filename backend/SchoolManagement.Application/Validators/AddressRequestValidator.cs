using FluentValidation;
using SchoolManagement.Application.DTOs.Addresses;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Application.Validators;

/// <summary>G1a: the shape of one address. Country-dependent rules are in AddressService.</summary>
public class AddressRequestValidator : AbstractValidator<AddressRequest>
{
    public AddressRequestValidator()
    {
        RuleFor(x => x.AddressType).Must(AddressTypes.IsValid)
            .WithMessage($"Address type must be one of: {string.Join(", ", AddressTypes.All)}.");
        RuleFor(x => x.Line1).NotEmpty().WithMessage("Address line 1 is required.").MaximumLength(200);
        RuleFor(x => x.Line2).MaximumLength(200);
        RuleFor(x => x.City).NotEmpty().WithMessage("City is required.").MaximumLength(60);
        RuleFor(x => x.CountryId).GreaterThan(0).WithMessage("Country is required.");
        RuleFor(x => x.PostalCode).MaximumLength(12);
    }
}

/// <summary>Rules for the whole list of an owner's addresses (the database enforces the same two).</summary>
public static class AddressListRules
{
    public static bool OnePerType(IEnumerable<AddressRequest>? list) =>
        list is null || list.GroupBy(a => a.AddressType).All(g => g.Count() == 1);

    /// <summary>A non-empty list has exactly one primary address.</summary>
    public static bool ExactlyOnePrimary(IEnumerable<AddressRequest>? list) =>
        list is null || !list.Any() || list.Count(a => a.IsPrimary) == 1;

    public const string OnePerTypeMessage = "Each address type (Home, Permanent, Mailing) can be used only once.";
    public const string OnePrimaryMessage = "Mark exactly one address as primary.";
}

/// <summary>Adds the address rules to any owner request validator.</summary>
public static class AddressValidationExtensions
{
    public static void ValidateAddresses<T>(this AbstractValidator<T> validator, System.Linq.Expressions.Expression<Func<T, IEnumerable<AddressRequest>?>> addresses)
    {
        validator.RuleForEach(addresses).SetValidator(new AddressRequestValidator());
        validator.RuleFor(addresses).Must(AddressListRules.OnePerType).WithMessage(AddressListRules.OnePerTypeMessage);
        validator.RuleFor(addresses).Must(AddressListRules.ExactlyOnePrimary).WithMessage(AddressListRules.OnePrimaryMessage);
    }
}
