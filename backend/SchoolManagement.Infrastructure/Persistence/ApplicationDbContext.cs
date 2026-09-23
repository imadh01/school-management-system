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
    }
}

