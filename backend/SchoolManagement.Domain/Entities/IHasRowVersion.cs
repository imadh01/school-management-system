namespace SchoolManagement.Domain.Entities;

/// <summary>
/// An entity that carries a version stamp. SQL Server changes the stamp automatically every time
/// the row is updated, so "save only if nobody changed this row since I read it" can be checked.
/// </summary>
public interface IHasRowVersion
{
    byte[] RowVersion { get; set; }
}