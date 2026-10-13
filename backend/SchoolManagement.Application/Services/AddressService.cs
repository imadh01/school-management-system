using System.Text.RegularExpressions;
using SchoolManagement.Application.DTOs.Addresses;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Exceptions;

namespace SchoolManagement.Application.Services;

/// <summary>
/// G1a. Shape rules (type, required fields, lengths, one per type, one primary) are checked by
/// AddressRequestValidator before the request gets here; this service checks what needs the
/// Countries table, and saves. The database constraints are the final safety net for both.
/// </summary>
public class AddressService : IAddressService
{
    private readonly IAddressRepository _repository;

    public AddressService(IAddressRepository repository) => _repository = repository;

    public async Task<List<CountryResponse>> GetCountriesAsync(CancellationToken cancellationToken)
    {
        var countries = await _repository.GetCountriesAsync(cancellationToken);
        return countries
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new CountryResponse(
                c.Id, c.Code, c.Name, c.RegionLabel,
                c.PostalCodeRequired, c.PostalCodePattern, c.PostalCodeHint,
                c.Regions.Where(r => r.IsActive)
                    .OrderBy(r => r.SortOrder).ThenBy(r => r.Name)
                    .Select(r => new CountryRegionResponse(r.Id, r.Name))
                    .ToList()))
            .ToList();
    }

    public async Task<List<Address>> BuildAsync(IReadOnlyList<AddressRequest> requests, CancellationToken cancellationToken)
    {
        if (requests.Count == 0) return new List<Address>();

        var countries = (await _repository.GetCountriesAsync(cancellationToken)).ToDictionary(c => c.Id);
        var errors = new Dictionary<string, List<string>>();
        void Fail(int index, string field, string message)
        {
            var key = $"Addresses[{index}].{field}";
            if (!errors.TryGetValue(key, out var list)) errors[key] = list = new List<string>();
            list.Add(message);
        }

        var result = new List<Address>();
        for (var i = 0; i < requests.Count; i++)
        {
            var r = requests[i];

            // Inactive countries and regions are hidden in the forms but still accepted here, so an
            // older address that uses one can be saved again unchanged.
            if (!countries.TryGetValue(r.CountryId, out var country))
            {
                Fail(i, nameof(AddressRequest.CountryId), "Choose a country from the list.");
                continue;
            }

            CountryRegion? region = null;
            if (r.RegionId is int regionId)
            {
                region = country.Regions.FirstOrDefault(x => x.Id == regionId);
                if (region is null)
                    Fail(i, nameof(AddressRequest.RegionId), $"Choose a {country.RegionLabel.ToLowerInvariant()} of {country.Name}.");
            }
            else if (country.Regions.Any(x => x.IsActive))
            {
                Fail(i, nameof(AddressRequest.RegionId), $"{country.RegionLabel} is required.");
            }

            var postalCode = Clean(r.PostalCode);
            if (postalCode is null)
            {
                if (country.PostalCodeRequired)
                    Fail(i, nameof(AddressRequest.PostalCode), $"Postcode is required for {country.Name}.");
            }
            else if (country.PostalCodePattern is not null && !Matches(postalCode, country.PostalCodePattern))
            {
                Fail(i, nameof(AddressRequest.PostalCode),
                    country.PostalCodeHint is null
                        ? $"This is not a valid postcode for {country.Name}."
                        : $"Postcode for {country.Name} must be {country.PostalCodeHint}.");
            }

            result.Add(new Address
            {
                AddressType = r.AddressType,
                IsPrimary = r.IsPrimary,
                Line1 = r.Line1.Trim(),
                Line2 = Clean(r.Line2),
                City = r.City.Trim(),
                PostalCode = postalCode,
                CountryId = country.Id,
                Country = country,
                RegionId = region?.Id,
                Region = region,
            });
        }

        if (errors.Count > 0)
            throw new RequestValidationException(errors.ToDictionary(e => e.Key, e => e.Value.ToArray()));
        return result;
    }

    public async Task ReplaceAsync(ICollection<Address> current, IReadOnlyList<Address> desired, CancellationToken cancellationToken)
    {
        var desiredByType = desired.ToDictionary(a => a.AddressType);
        var primaryType = desired.FirstOrDefault(a => a.IsPrimary)?.AddressType;

        // Step 1: remove the types that are gone, and take "primary" off a row that loses it, so the
        // unique indexes (one per type, one primary) never see the old and the new row at once.
        var step1 = false;
        foreach (var existing in current.ToList())
        {
            if (!desiredByType.ContainsKey(existing.AddressType))
            {
                current.Remove(existing);
                _repository.Remove(existing);
                step1 = true;
            }
            else if (existing.IsPrimary && existing.AddressType != primaryType)
            {
                existing.IsPrimary = false;
                step1 = true;
            }
        }
        if (step1) await _repository.SaveChangesAsync(cancellationToken);

        // Step 2: update the rows that stay (same type = same row, so its id and audit history
        // carry on) and add the new ones.
        foreach (var wanted in desired)
        {
            var existing = current.FirstOrDefault(a => a.AddressType == wanted.AddressType);
            if (existing is null)
            {
                current.Add(wanted);
                continue;
            }
            existing.IsPrimary = wanted.IsPrimary;
            existing.Line1 = wanted.Line1;
            existing.Line2 = wanted.Line2;
            existing.City = wanted.City;
            existing.PostalCode = wanted.PostalCode;
            existing.CountryId = wanted.CountryId;
            existing.Country = wanted.Country;
            existing.RegionId = wanted.RegionId;
            existing.Region = wanted.Region;
        }
        // The owner's own save writes step 2 together with its other changes.
    }

    private static bool Matches(string value, string pattern)
    {
        try
        {
            return Regex.IsMatch(value, pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
