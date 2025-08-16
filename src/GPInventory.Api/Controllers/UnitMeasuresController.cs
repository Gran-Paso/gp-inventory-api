using GPInventory.Application.DTOs.Production;
using GPInventory.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GPInventory.Api.Controllers;

[ApiController]
[Route("api/unit-measures")]
[Authorize]
public class UnitMeasuresController : ControllerBase
{
    private readonly IUnitMeasureService _unitMeasureService;

    public UnitMeasuresController(IUnitMeasureService unitMeasureService)
    {
        _unitMeasureService = unitMeasureService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<UnitMeasureDto>>> GetAll()
    {
        try
        {
            var unitMeasures = await _unitMeasureService.GetAllAsync();
            return Ok(unitMeasures);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<UnitMeasureDto>> GetById(int id)
    {
        try
        {
            var unitMeasure = await _unitMeasureService.GetByIdAsync(id);
            if (unitMeasure == null)
                return NotFound($"UnitMeasure with id {id} not found");

            return Ok(unitMeasure);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    [HttpPost]
    public async Task<ActionResult<UnitMeasureDto>> Create(CreateUnitMeasureDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var unitMeasure = await _unitMeasureService.CreateAsync(createDto);
            return CreatedAtAction(nameof(GetById), new { id = unitMeasure.Id }, unitMeasure);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<UnitMeasureDto>> Update(int id, UpdateUnitMeasureDto updateDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var unitMeasure = await _unitMeasureService.UpdateAsync(id, updateDto);
            return Ok(unitMeasure);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await _unitMeasureService.DeleteAsync(id);
            return NoContent();
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }
}
