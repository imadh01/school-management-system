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

        // Seed the six confirmed roles — matches database/scripts/01-create-tables-auth.sql
        modelBuilder.Entity<Role>().HasData(
            new Role { Id = 1, Name = "Admin" },
            new Role { Id = 2, Name = "Supervisor" },
            new Role { Id = 3, Name = "Clerk" },
            new Role { Id = 4, Name = "Teacher" },
            new Role { Id = 5, Name = "Student" },
            new Role { Id = 6, Name = "Parent" }
        );

        modelBuilder.Entity<Permission>().HasData(new Permission
        {
            Id = 1,
            Name = "Users.Create",
            Module = "Users",
            Description = "Create new user accounts.",
        });

        modelBuilder.Entity<RolePermission>().HasData(new RolePermission
        {
            RoleId = 1,   // Admin
            PermissionId = 1,
        });

        modelBuilder.Entity<Permission>().HasData(new Permission
        {
            Id = 2,
            Name = "ClassSections.Create",
            Module = "ClassSections",
            Description = "Create new class sections.",
        });

        modelBuilder.Entity<RolePermission>().HasData(new RolePermission
        {
            RoleId = 1, // Admin
            PermissionId = 2,
        });

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

        modelBuilder.Entity<Permission>().HasData(new Permission
        {
            Id = 3,
            Name = "Admissions.Manage",
            Module = "Admissions",
            Description = "Create, edit, and progress admission applications through the pipeline.",
        });

        modelBuilder.Entity<RolePermission>().HasData(new RolePermission
        {
            RoleId = 1, // Admin
            PermissionId = 3,
        });

        modelBuilder.Entity<Permission>().HasData(new Permission
        {
            Id = 4,
            Name = "Students.Manage",
            Module = "Students",
            Description = "Create and edit student records, including converting enrolled admissions.",
        });

        modelBuilder.Entity<RolePermission>().HasData(new RolePermission
        {
            RoleId = 1, // Admin
            PermissionId = 4,
        });
        modelBuilder.Entity<Permission>().HasData(new Permission
        {
            Id = 5,
            Name = "Parents.Manage",
            Module = "Parents",
            Description = "Create and edit parent records, and link/unlink guardians to students.",
        });

        modelBuilder.Entity<RolePermission>().HasData(new RolePermission
        {
            RoleId = 1,
            PermissionId = 5,
        });
        modelBuilder.Entity<Permission>().HasData(new Permission
        {
            Id = 6,
            Name = "Subjects.Manage",
            Module = "Subjects",
            Description = "Create and edit subjects, including their marks configuration.",
        });

        modelBuilder.Entity<Permission>().HasData(new Permission
        {
            Id = 8,
            Name = "Teachers.Manage",
            Module = "Teachers",
            Description = "Create, edit and delete teachers; assign subjects and class teachers.",
        });

        modelBuilder.Entity<RolePermission>().HasData(new RolePermission
        {
            RoleId = 1, // Admin
            PermissionId = 8,
        });

        modelBuilder.Entity<RolePermission>().HasData(new RolePermission
        {
            RoleId = 1, // Admin
            PermissionId = 6,
        });
        modelBuilder.Entity<Permission>().HasData(new Permission
        {
            Id = 7,
            Name = "ClassSections.Manage",
            Module = "ClassSections",
            Description = "Create, edit, activate/deactivate and delete class sections.",
        });

        modelBuilder.Entity<RolePermission>().HasData(new RolePermission
        {
            RoleId = 1, // Admin
            PermissionId = 7,
        });

        modelBuilder.Entity<Permission>().HasData(
        new Permission
        {
            Id = 9,
            Name = "Attendance.Manage",
            Module = "Attendance",
            Description = "Mark and edit attendance for any class."
        },
        new Permission
        {
            Id = 10,
            Name = "Attendance.Mark",
            Module = "Attendance",
            Description = "Mark attendance for own class or subjects (checked in the service)."
        });

        modelBuilder.Entity<RolePermission>().HasData(
            new RolePermission { RoleId = 1, PermissionId = 9 },   // Admin
            new RolePermission { RoleId = 2, PermissionId = 9 },   // Supervisor
            new RolePermission { RoleId = 3, PermissionId = 9 },   // Clerk
            new RolePermission { RoleId = 4, PermissionId = 10 }); // Teacher

        // Full Aadhaar / passport / visa. Without this permission the API returns masked values only.
        modelBuilder.Entity<Permission>().HasData(new Permission
        {
            Id = 11,
            Name = "Students.ViewSensitive",
            Module = "Students",
            Description = "View and edit full Aadhaar, passport and visa details of students."
        });

        modelBuilder.Entity<RolePermission>().HasData(
            new RolePermission { RoleId = 1, PermissionId = 11 },   // Admin
            new RolePermission { RoleId = 2, PermissionId = 11 });  // Supervisor
    }
}