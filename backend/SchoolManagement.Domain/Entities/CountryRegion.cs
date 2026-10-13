namespace SchoolManagement.Domain.Entities;

/// <summary>G1a: a state, emirate or province of a country, offered as a list in address forms.</summary>
public class CountryRegion
{
    public int Id { get; set; }

    public int CountryId { get; set; }
    public Country Country { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
