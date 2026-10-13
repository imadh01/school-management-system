using SchoolManagement.Application.DTOs.Addresses;

namespace SchoolManagement.Application.DTOs.Parents;

public record ParentResponse(
    int Id, string Name, string? Email, string Mobile, string Status,
    string? Occupation, string? Nationality, string? CountryOfResidence, string? Timezone,
    string? PreferredLanguage, string? PreferredContactMethod, string? Whatsapp, bool EmergencyOnly,
    bool NotifyAttendance, bool NotifyExams, bool NotifyFees, bool NotifyNotices, bool NotifyDiscipline,
    string? Employer, string? JobTitle, string? WorkEmail, string? WorkPhone, bool BillingContact,
    List<AddressResponse> Addresses, string? LegacyAddress,
    string RowVersion);