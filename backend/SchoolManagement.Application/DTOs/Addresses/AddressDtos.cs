namespace SchoolManagement.Application.DTOs.Addresses;

/// <summary>
/// G1a: one address in a student / parent / application request. The owner's request carries the
/// whole list: it replaces the stored addresses (null = leave them as they are, [] = remove all).
/// </summary>
public record AddressRequest(
    string AddressType, bool IsPrimary,
    string Line1, string? Line2, string City,
    int CountryId, int? RegionId, string? PostalCode);

public record AddressResponse(
    int Id, string AddressType, bool IsPrimary,
    string Line1, string? Line2, string City,
    int CountryId, string CountryName,
    int? RegionId, string? RegionName,
    string? PostalCode);

/// <summary>A country offered in address forms, with its regions and postcode rule.</summary>
public record CountryResponse(
    int Id, string Code, string Name, string RegionLabel,
    bool PostalCodeRequired, string? PostalCodePattern, string? PostalCodeHint,
    List<CountryRegionResponse> Regions);

public record CountryRegionResponse(int Id, string Name);
