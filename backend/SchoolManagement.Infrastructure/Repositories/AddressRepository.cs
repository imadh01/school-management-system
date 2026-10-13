using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Infrastructure.Repositories;

public class AddressRepository : IAddressRepository
{
    private readonly ApplicationDbContext _context;

    public AddressRepository(ApplicationDbContext context) => _context = context;

    // A handful of countries with a few dozen regions: loading them whole is cheaper than a query per address.
    public Task<List<Country>> GetCountriesAsync(CancellationToken cancellationToken) =>
        _context.Countries.Include(c => c.Regions).ToListAsync(cancellationToken);

    public void Remove(Address address) => _context.Addresses.Remove(address);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => _context.SaveChangesAsync(cancellationToken);
}
