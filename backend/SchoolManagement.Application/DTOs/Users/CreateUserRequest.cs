using SchoolManagement.Application.DTOs.Addresses;

namespace SchoolManagement.Application.DTOs.Users;

/// <summary>
/// E2: a new staff account with its profile. The password is temporary: the account always starts with
/// MustChangePassword. Role "Teacher" also creates the Teachers row (Specialization goes there).
/// Student and Parent are refused until the portal exists (decision 1).
/// </summary>
public record CreateUserRequest(
    string Username,
    string Email,
    string Password,
    string RoleName,
    StaffProfileRequest Profile,
    List<AddressRequest>? Addresses = null,
    string? Specialization = null);