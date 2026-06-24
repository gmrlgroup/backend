using Application.DailyInventory.Services;
using Application.Shared.Authorization;
using Application.Shared.Models.DailyInventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Application.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Policy = PolicyNames.InventoryRead)]
public class DailyInventoryController : ControllerBase
{
    private readonly IDailyInventoryService _service;

    public DailyInventoryController(IDailyInventoryService service)
    {
        _service = service;
    }

    [HttpGet("locations")]
    public async Task<ActionResult<List<DailyInventoryLocation>>> GetLocations(
        [FromHeader(Name = "X-Company-Id")] string? companyId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(companyId))
            return BadRequest("Company ID is required in headers");

        if (!User.HasCompanyRole(companyId, RoleSuffixes.InventoryRead))
            return Forbid();

        var locations = await _service.GetLocationsAsync(companyId, cancellationToken);
        return Ok(locations);
    }

    [HttpGet]
    public async Task<ActionResult<List<DailyInventoryRow>>> Get(
        [FromHeader(Name = "X-Company-Id")] string? companyId,
        [FromQuery] DateTime? endDate          = null,
        [FromQuery] string? itemNo             = null,
        [FromQuery] string? variantCode        = null,
        [FromQuery] List<string>? locationCodes = null,
        [FromQuery] bool hasStock              = false,
        [FromQuery] bool hasSales              = false,
        [FromQuery] int offset                 = 0,
        [FromQuery] int limit                  = 1000,
        CancellationToken cancellationToken     = default)
    {
        if (string.IsNullOrWhiteSpace(companyId))
            return BadRequest("Company ID is required in headers");

        if (!User.HasCompanyRole(companyId, RoleSuffixes.InventoryRead))
            return Forbid();

        var query = new DailyInventoryQuery
        {
            CompanyId     = companyId,
            EndDate       = endDate ?? DateTime.Today,
            ItemNo        = itemNo,
            VariantCode   = variantCode,
            LocationCodes = locationCodes,
            HasStock      = hasStock,
            HasSales      = hasSales,
            Offset        = offset,
            Limit         = Math.Clamp(limit, 1, 50000)
        };

        var rows = await _service.GetDailyInventoryAsync(query, cancellationToken);
        return Ok(rows);
    }

    /// <summary>Available item_details columns the user can add to the grid (discovered via DESCRIBE).</summary>
    [HttpGet("detail-columns")]
    public async Task<ActionResult<List<string>>> GetDetailColumns(
        [FromHeader(Name = "X-Company-Id")] string? companyId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(companyId))
            return BadRequest("Company ID is required in headers");

        if (!User.HasCompanyRole(companyId, RoleSuffixes.InventoryRead))
            return Forbid();

        return Ok(await _service.GetDetailColumnsAsync(cancellationToken));
    }

    /// <summary>Fetches selected item_details columns for a set of items, to be joined client-side.</summary>
    [HttpPost("details")]
    public async Task<ActionResult<List<ItemDetailsRow>>> GetItemDetails(
        [FromHeader(Name = "X-Company-Id")] string? companyId,
        [FromBody] ItemDetailsRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(companyId))
            return BadRequest("Company ID is required in headers");

        if (!User.HasCompanyRole(companyId, RoleSuffixes.InventoryRead))
            return Forbid();

        var details = await _service.GetItemDetailsAsync(
            companyId, request?.Columns ?? new(), request?.Items ?? new(), cancellationToken);
        return Ok(details);
    }

    /// <summary>
    /// Streams the full filtered result set (no pagination, first 1,000,000 rows) as a CSV download.
    /// Company id travels as a query parameter so the browser can trigger a plain anchor download
    /// (cookie auth still applies); selected item_details columns are included when supplied.
    /// </summary>
    [HttpGet("export")]
    public async Task ExportCsv(
        [FromQuery] string? companyId,
        [FromQuery] DateTime? endDate          = null,
        [FromQuery] string? itemNo             = null,
        [FromQuery] string? variantCode        = null,
        [FromQuery] List<string>? locationCodes = null,
        [FromQuery] bool hasStock              = false,
        [FromQuery] bool hasSales              = false,
        [FromQuery] List<string>? detailColumns = null,
        CancellationToken cancellationToken     = default)
    {
        if (string.IsNullOrWhiteSpace(companyId))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        if (!User.HasCompanyRole(companyId, RoleSuffixes.InventoryRead))
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        var query = new DailyInventoryQuery
        {
            CompanyId     = companyId,
            EndDate       = endDate ?? DateTime.Today,
            ItemNo        = itemNo,
            VariantCode   = variantCode,
            LocationCodes = locationCodes,
            HasStock      = hasStock,
            HasSales      = hasSales
        };

        var fileName = $"daily-inventory-{DateTime.Now:yyyyMMdd-HHmmss}.csv";
        Response.ContentType = "text/csv; charset=utf-8";
        Response.Headers.Append("Content-Disposition", $"attachment; filename=\"{fileName}\"");

        await _service.ExportCsvAsync(query, detailColumns ?? new(), Response.Body, cancellationToken);
    }
}
