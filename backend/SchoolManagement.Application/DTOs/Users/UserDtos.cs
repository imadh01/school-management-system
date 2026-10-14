using SchoolManagement.Application.DTOs.Addresses;

namespace SchoolManagement.Application.DTOs.Users;

/// <summary>The person behind a staff login, as the forms send it.</summary>
public record StaffProfileRequest(
    string FullName,
    string? Gender,
    DateOnly? DateOfBirth,
    string? Mobile,
    DateOnly? JoiningDate,
    string? Qualification,
    string? EmployeeNumber,
    string? BloodGroup,
    string? EmergencyContactName,
    string? EmergencyContactPhone);

public record StaffProfileResponse(
    string FullName,
    // True while the name is still the username copied in by the E2a migration (decision 3).
    bool NeedsName,
    string? Gender,
    DateOnly? DateOfBirth,
    string? Mobile,
    DateOnly? JoiningDate,
    string? Qualification,
    string? EmployeeNumber,
    string? BloodGroup,
    string? EmergencyContactName,
    string? EmergencyContactPhone);

/// <summary>
/// E2: an edit of an account. Addresses: null = keep the stored ones. Changing RoleName ends the user's
/// sessions (same rules as the account service: not your own, not the last active Admin, ...).
/// </summary>
public record UpdateUserRequest(
    string Username,
    string Email,
    string RoleName,
    StaffProfileRequest Profile,
    List<AddressRequest>? Addresses,
    string RowVersion);

public record ChangeUserStatusRequest(string Status, string RowVersion);

/// <summary>A temporary password typed by an administrator; the user must change it at the next sign-in.</summary>
public record ResetUserPasswordRequest(string NewPassword);

/// <summary>One row of the Users list.</summary>
public record UserListItemResponse(
    int Id,
    string Username,
    string FullName,
    bool NeedsName,
    string Email,
    string? Mobile,
    string? Role,
    string? Gender,
    string Status,
    DateOnly? JoiningDate,
    DateTime? LastLoginAt,
    bool IsLocked,
    bool MustChangePassword,
    int? TeacherId,
    string? EmployeeNumber);

/// <summary>
/// E2: My Account. What you may change about yourself: your e-mail and your profile, not your
/// username, role, status, joining date or employee number (an administrator sets those).
/// </summary>
public record UpdateMyProfileRequest(
    string Email,
    string FullName,
    string? Gender,
    DateOnly? DateOfBirth,
    string? Mobile,
    string? Qualification,
    string? BloodGroup,
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    List<AddressRequest>? Addresses,
    string RowVersion);
