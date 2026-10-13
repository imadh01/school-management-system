namespace SchoolManagement.Domain.Entities;

/// <summary>
/// G1a: a country offered in address forms, with its own address rules. Countries and their
/// regions are data, not code: a new country is added with rows (later from the Settings page),
/// no release needed.
/// </summary>
public class Country
{
    public int Id { get; set; }

    /// <summary>ISO 3166-1 alpha-2, e.g. "IN", "AE". Stable key for seed data and code.</summary>
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    /// <summary>What a region is called in this country: "Emirate", "State".</summary>
    public string RegionLabel { get; set; } = "State";

    public bool PostalCodeRequired { get; set; }
    /// <summary>Regular expression a postal code must match, or null for no format check.</summary>
    public string? PostalCodePattern { get; set; }
    /// <summary>Shown when the format is wrong, e.g. "6 digits".</summary>
    public string? PostalCodeHint { get; set; }

    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<CountryRegion> Regions { get; set; } = new List<CountryRegion>();
}
