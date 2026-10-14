using SchoolManagement.Application.DTOs.Addresses;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Application.Services;

/// <summary>G1a: addresses in responses, and the copy made at enrolment.</summary>
public static class AddressMapping
{
    /// <summary>Primary first, then Home, Permanent, Mailing. Country and Region must be loaded.</summary>
    public static List<AddressResponse> ToResponses(IEnumerable<Address> addresses) =>
        addresses
            .OrderByDescending(a => a.IsPrimary)
            .ThenBy(a => Array.IndexOf(AddressTypes.All, a.AddressType))
            .Select(a => new AddressResponse(
                a.Id, a.AddressType, a.IsPrimary,
                a.Line1, a.Line2, a.City,
                a.CountryId, a.Country?.Name ?? string.Empty,
                a.RegionId, a.Region?.Name,
                a.PostalCode))
            .ToList();

    /// <summary>New rows with the same content (the application keeps its own; the student gets copies).</summary>
    public static List<Address> CopyOf(IEnumerable<Address> addresses) =>
        addresses.Select(a => new Address
        {
            AddressType = a.AddressType,
            IsPrimary = a.IsPrimary,
            Line1 = a.Line1,
            Line2 = a.Line2,
            City = a.City,
            PostalCode = a.PostalCode,
            CountryId = a.CountryId,
            RegionId = a.RegionId,
        }).ToList();
}
