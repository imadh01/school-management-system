using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Application.Interfaces;

public interface IAddressRepository
{
    /// <summary>All countries with their regions, tracked (addresses being saved point at these rows).</summary>
    Task<List<Country>> GetCountriesAsync(CancellationToken cancellationToken);

    /// <summary>Marks an address for deletion (removing it from the owner's list is not enough: the owner FKs are optional).</summary>
    void Remove(Address address);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
