namespace SchoolManagement.Application.DTOs.Parents;

/// <summary>
/// A parent as a caller with only Parents.ViewScoped sees them (Batch F1): name and mobile.
/// No email, employer, work contact, address or notification settings. Same property names as
/// <see cref="ParentResponse"/>, so it is a strict subset.
/// </summary>
public record ParentContactResponse(int Id, string Name, string Mobile);
