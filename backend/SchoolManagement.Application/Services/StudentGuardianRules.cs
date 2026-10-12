using SchoolManagement.Application.DTOs.Students;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Application.Services;

/// <summary>Which of a student's guardians a scoped caller may see (Batch F1). Used by every guardian read.</summary>
public static class StudentGuardianRules
{
    /// <summary>
    /// A teacher of the student (or the student themself) sees every guardian, as contacts.
    /// A caller who reaches the student ONLY as a parent sees just their own link: co-guardians stay
    /// private, because separated parents and custody arrangements exist.
    /// </summary>
    public static IEnumerable<StudentGuardian> VisibleLinks(
        IEnumerable<StudentGuardian> links, StudentAccess access, DataScope scope)
    {
        if ((access & (StudentAccess.Unrestricted | StudentAccess.Teaches | StudentAccess.Self)) != 0)
            return links;

        return links.Where(l => l.Parent.UserId is not null && l.Parent.UserId == scope.UserId);
    }

    public static GuardianContactResponse ToContact(StudentGuardian link) => new(
        link.ParentId, link.Parent.Name, link.Parent.Mobile, link.RelationType, link.IsPrimaryContact);
}
