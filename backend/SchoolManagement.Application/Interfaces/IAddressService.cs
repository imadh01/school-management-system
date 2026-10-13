using SchoolManagement.Application.DTOs.Addresses;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Application.Interfaces;

/// <summary>
/// G1a: the one place that turns address requests into rows and saves them, for every owner
/// (students, parents, applications; staff in E2).
/// </summary>
public interface IAddressService
{
    /// <summary>Active countries and their active regions, for the address forms.</summary>
    Task<List<CountryResponse>> GetCountriesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Checks the rules that depend on the country (country exists, region belongs to it and is
    /// required when it has regions, postcode required / format) and builds the rows. Throws
    /// RequestValidationException with keys like "Addresses[0].PostalCode".
    /// </summary>
    Task<List<Address>> BuildAsync(IReadOnlyList<AddressRequest> requests, CancellationToken cancellationToken);

    /// <summary>
    /// Makes the owner's stored addresses equal to <paramref name="desired"/>, matching rows by
    /// address type. May save twice (so the one-primary / one-per-type indexes never see old and
    /// new rows together): call it inside the owner's transaction.
    /// </summary>
    Task ReplaceAsync(ICollection<Address> current, IReadOnlyList<Address> desired, CancellationToken cancellationToken);
}
