using SchoolManagement.Application.DTOs.Addresses;

namespace SchoolManagement.Application.DTOs.Users;

/// <summary>E2: one account on the Users detail page and edit form.</summary>
public record UserResponse(
    int Id,
    string Username,
    string Email,
    string Status,
    IReadOnlyList<string> Roles,
    bool MustChangePassword,
    bool IsLocked,
    DateTime? LastLoginAt,
    DateTime CreatedAt,
    // The teacher record, when the user is a teacher (links to the Teachers page).
    int? TeacherId,
    // Null only for an account that has no staff profile (student/parent logins later).
    StaffProfileResponse? Profile,
    List<AddressResponse> Addresses,
    string RowVersion);