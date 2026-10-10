using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Application.DTOs.Roles;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Constants;

namespace SchoolManagement.API.Controllers;

/// <summary>The permission catalog, for the Roles screen (Batch E1). Read-only: permissions are defined in code.</summary>
[ApiController]
[Route("api/permissions")]
[Authorize]
public class PermissionsController : ControllerBase
{
    private readonly IRoleService _roleService;

    public PermissionsController(IRoleService roleService)
    {
        _roleService = roleService;
    }

    /// <summary>Every permission grouped by module, each with the permissions it requires.</summary>
    [HttpGet]
    [Authorize(Policy = Permissions.RolesView)]
    [ProducesResponseType(typeof(IReadOnlyList<PermissionModuleResponse>), StatusCodes.Status200OK)]
    public IActionResult GetCatalog() => Ok(_roleService.GetPermissionCatalog());
}
