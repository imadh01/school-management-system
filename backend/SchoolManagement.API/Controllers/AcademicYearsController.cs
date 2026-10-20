using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Constants;

namespace SchoolManagement.API.Controllers;

/// <summary>
/// Read-only list of academic years, for the Timetable's year dropdown. Creating, closing and switching years
/// is a separate module (see docs/design/academic-years.md). Until then Timetable.View is enough to read the list.
/// </summary>
[ApiController]
[Route("api/academic-years")]
[Authorize]
public class AcademicYearsController : ControllerBase
{
    private readonly IAcademicYearService _academicYearService;

    public AcademicYearsController(IAcademicYearService academicYearService)
    {
        _academicYearService = academicYearService;
    }

    [HttpGet]
    [Authorize(Policy = Permissions.TimetableView)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        Ok(await _academicYearService.GetAllAsync(null, cancellationToken));
}
