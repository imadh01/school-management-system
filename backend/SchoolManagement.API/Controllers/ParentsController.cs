using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FluentValidation;
using SchoolManagement.API.Common;
using SchoolManagement.API.Extensions;
using SchoolManagement.Application.DTOs.Parents;
using SchoolManagement.Application.Interfaces;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Route("api/parents")]
public class ParentsController : ControllerBase
{
    private readonly IParentService _parentService;
    private readonly IValidator<CreateParentRequest> _createValidator;
    private readonly IValidator<UpdateParentRequest> _updateValidator;

    public ParentsController(
        IParentService parentService,
        IValidator<CreateParentRequest> createValidator,
        IValidator<UpdateParentRequest> updateValidator)
    {
        _parentService = parentService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        Ok(await _parentService.GetAllAsync(cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken) =>
        Ok(await _parentService.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    [Authorize(Policy = "Parents.Manage")]
    public async Task<IActionResult> Create(CreateParentRequest request, CancellationToken cancellationToken)
    {
        var validationResult = await _createValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return BadRequest(new ApiErrorResponse
            {
                Message = "Validation failed.",
                ErrorCode = "VALIDATION_ERROR",
                Errors = validationResult.ToErrorDictionary(),
                TraceId = HttpContext.GetCorrelationId(),
            });
        }

        var result = await _parentService.CreateAsync(request, cancellationToken);
        return Created($"api/parents/{result.Id}", result);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = "Parents.Manage")]
    public async Task<IActionResult> Update(int id, UpdateParentRequest request, CancellationToken cancellationToken)
    {
        var validationResult = await _updateValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return BadRequest(new ApiErrorResponse
            {
                Message = "Validation failed.",
                ErrorCode = "VALIDATION_ERROR",
                Errors = validationResult.ToErrorDictionary(),
                TraceId = HttpContext.GetCorrelationId(),
            });
        }

        return Ok(await _parentService.UpdateAsync(id, request, cancellationToken));
    }
}
