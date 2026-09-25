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
    }
}

