using SchoolManagement.Application.DTOs.Roles;

namespace SchoolManagement.Application.Interfaces;

public interface IRoleService
{
    /// <summary>The whole catalog grouped by module, with each permission's dependencies.</summary>
    IReadOnlyList<PermissionModuleResponse> GetPermissionCatalog();

    Task<IReadOnlyList<RoleSummaryResponse>> GetAllAsync(CancellationToken cancellationToken);

    Task<RoleResponse> GetByIdAsync(int id, CancellationToken cancellationToken);

    Task<RoleResponse> CreateAsync(CreateRoleRequest request, CancellationToken cancellationToken);

    Task<RoleResponse> UpdateAsync(int id, UpdateRoleRequest request, CancellationToken cancellationToken);

    Task<RoleResponse> SetPermissionsAsync(int id, SetRolePermissionsRequest request, CancellationToken cancellationToken);

    Task<RoleResponse> ChangeStatusAsync(int id, ChangeRoleStatusRequest request, CancellationToken cancellationToken);
}
