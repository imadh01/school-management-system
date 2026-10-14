namespace SchoolManagement.Application.DTOs.Teachers;

/// <summary>
/// E3a: the teaching profile only. Username, e-mail, status and password belong to the login and are
/// changed on the Users page (decision 7). Name and phone are also written to the staff profile.
/// </summary>
public record UpdateTeacherRequest(string Name, string? Phone, string? Specialization);