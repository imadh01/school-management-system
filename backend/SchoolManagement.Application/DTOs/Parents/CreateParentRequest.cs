namespace SchoolManagement.Application.DTOs.Parents;

public record CreateParentRequest(
    string Name, string? Email, string Mobile,
    string? Occupation, string? Nationality, string? CountryOfResidence, string? Timezone,
    string? PreferredLanguage, string? PreferredContactMethod, string? Whatsapp, bool EmergencyOnly,
    bool NotifyAttendance, bool NotifyExams, bool NotifyFees, bool NotifyNotices, bool NotifyDiscipline,
    string? Employer, string? JobTitle, string? WorkEmail, string? WorkPhone, bool BillingContact,
    string? AddressLine, string? City, string? State, string? Pincode);