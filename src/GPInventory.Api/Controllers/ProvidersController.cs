using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using GPInventory.Application.DTOs.Production;
using GPInventory.Application.Interfaces;

namespace GPInventory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableCors("AllowFrontend")]
public class ProvidersController : ControllerBase
{
    private readonly IProviderService _providerService;
    private readonly ILogger<ProvidersController> _logger;

    public ProvidersController(IProviderService providerService, ILogger<ProvidersController> logger)
    {
        _providerService = providerService;
        _logger = logger;
    }

    /// <summary>
    /// Get all providers or filter by business
    /// </summary>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<IEnumerable<ProviderDto>>> GetProviders([FromQuery] int? businessId = null)
    {
        try
        {
            _logger.LogInformation("Getting providers with filters");

            IEnumerable<ProviderDto> providers;

            if (businessId.HasValue)
            {
                providers = await _providerService.GetProvidersByBusinessIdAsync(businessId.Value);
            }
            else
            {
                providers = await _providerService.GetAllProvidersAsync();
            }

            _logger.LogInformation($"Found {providers.Count()} providers");
            return Ok(providers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving providers");
            return StatusCode(500, new { message = "Error retrieving providers", error = ex.Message });
        }
    }

    /// <summary>
    /// Get provider by ID
    /// </summary>
    [HttpGet("{id}")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<ProviderDto>> GetProvider(int id)
    {
        try
        {
            _logger.LogInformation("Getting provider with ID: {id}", id);

            var provider = await _providerService.GetProviderByIdAsync(id);
            return Ok(provider);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving provider with ID: {id}", id);
            return StatusCode(500, new { message = "Error retrieving provider", error = ex.Message });
        }
    }

    /// <summary>
    /// Get providers by business ID
    /// </summary>
    [HttpGet("business/{businessId}")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<IEnumerable<ProviderDto>>> GetProvidersByBusiness(int businessId)
    {
        try
        {
            _logger.LogInformation("Getting providers for business ID: {businessId}", businessId);

            var providers = await _providerService.GetProvidersByBusinessIdAsync(businessId);

            _logger.LogInformation($"Found {providers.Count()} providers for business {businessId}");
            return Ok(providers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving providers for business ID: {businessId}", businessId);
            return StatusCode(500, new { message = "Error retrieving providers", error = ex.Message });
        }
    }

    /// <summary>
    /// Create a new provider
    /// </summary>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(409)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<ProviderDto>> CreateProvider([FromBody] CreateProviderDto createProviderDto)
    {
        try
        {
            _logger.LogInformation("Creating new provider: {providerName}", createProviderDto.Name);

            var provider = await _providerService.CreateProviderAsync(createProviderDto);

            _logger.LogInformation("Provider created successfully: {providerName} with ID: {providerId}", provider.Name, provider.Id);
            return CreatedAtAction(nameof(GetProvider), new { id = provider.Id }, provider);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating provider");
            return StatusCode(500, new { message = "Error creating provider", error = ex.Message });
        }
    }

    /// <summary>
    /// Update an existing provider
    /// </summary>
    [HttpPut("{id}")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<ProviderDto>> UpdateProvider(int id, [FromBody] UpdateProviderDto updateProviderDto)
    {
        try
        {
            _logger.LogInformation("Updating provider with ID: {id}", id);

            var provider = await _providerService.UpdateProviderAsync(id, updateProviderDto);

            _logger.LogInformation("Provider updated successfully: {providerName} with ID: {id}", provider.Name, id);
            return Ok(provider);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating provider with ID: {id}", id);
            return StatusCode(500, new { message = "Error updating provider", error = ex.Message });
        }
    }

    /// <summary>
    /// Delete a provider
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<ActionResult> DeleteProvider(int id)
    {
        try
        {
            _logger.LogInformation("Deleting provider with ID: {id}", id);

            await _providerService.DeleteProviderAsync(id);

            _logger.LogInformation("Provider deleted successfully with ID: {id}", id);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting provider with ID: {id}", id);
            return StatusCode(500, new { message = "Error deleting provider", error = ex.Message });
        }
    }

    /// <summary>
    /// Get providers by store ID
    /// </summary>
    [HttpGet("store/{storeId}")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<IEnumerable<ProviderDto>>> GetProvidersByStore(int storeId)
    {
        try
        {
            var providers = await _providerService.GetProvidersByStoreIdAsync(storeId);
            return Ok(providers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving providers for store: {storeId}", storeId);
            return StatusCode(500, new { message = "Error retrieving providers", error = ex.Message });
        }
    }
}
