using Microsoft.EntityFrameworkCore;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Persistence.Interceptors;

namespace SchoolManagement.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext
{
    private readonly AuditableEntitySaveChangesInterceptor _auditInterceptor;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        AuditableEntitySaveChangesInterceptor auditInterceptor) : base(options)
    {
        _auditInterceptor = auditInterceptor;
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<AcademicYear> AcademicYears => Set<AcademicYear>();
    public DbSet<ClassSection> ClassSections => Set<ClassSection>();
    public DbSet<Admission> Admissions => Set<Admission>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Parent> Parents => Set<Parent>();
    public DbSet<StudentGuardian> StudentGuardians => Set<StudentGuardian>();
    public DbSet<AdmissionGuardian> AdmissionGuardians => Set<AdmissionGuardian>();
    public DbSet<StudentHealth> StudentHealth => Set<StudentHealth>();
    public DbSet<StudentIdentityDocument> StudentIdentityDocuments => Set<StudentIdentityDocument>();
    public DbSet<StudentPickupPerson> StudentPickupPersons => Set<StudentPickupPerson>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<Teacher> Teachers => Set<Teacher>();
    public DbSet<AttendanceSession> AttendanceSessions => Set<AttendanceSession>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<StudentEnrollment> StudentEnrollments => Set<StudentEnrollment>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.AddInterceptors(_auditInterceptor);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // E1: Roles, Permissions and RolePermissions are no longer seeded with HasData.
        //   - Permissions mirror Domain.Constants.Permissions; migrations insert new names with SQL
        //     keyed by name, and a test checks the table equals the code catalog.
        //   - Roles and their grants are runtime data edited through the Roles API; a HasData seed
        //     would let a later migration re-insert or delete rows an admin changed.
        // The rows themselves were inserted by earlier migrations and stay where they are.

        modelBuilder.Entity<AcademicYear>().HasData(new AcademicYear
        {
            Id = 1,
            Name = "2026-2027",
            StartDate = new DateOnly(2026, 6, 1),
            EndDate = new DateOnly(2027, 3, 31),
            IsCurrent = true,
            Status = "Active",
            CreatedAt = new DateTime(2026, 9, 23, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 9, 23, 0, 0, 0, DateTimeKind.Utc),
        });
    }

}