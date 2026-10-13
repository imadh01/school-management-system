using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Application.DTOs.Addresses;
using SchoolManagement.Application.Interfaces;

namespace SchoolManagement.API.Controllers;

/// <summary>
/// G1a: the countries offered in address forms, each with its regions (emirates / states) and
/// postcode rule. Reference data for every form that has an address, so any signed-in user may read it.
/// </summary>
[ApiController]
[Route("api/countries")]
[Authorize]
public class CountriesController : ControllerBase
{
    private readonly IAddressService _addressService;

    public CountriesController(IAddressService addressService) => _addressService = addressService;

    [HttpGet]
    [ProducesResponseType(typeof(List<CountryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        Ok(await _addressService.GetCountriesAsync(cancellationToken));
}
