using Application.Shared.Models;
using Application.Shared.Services;
using Application.Shared.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Application.Controllers;

[Route("api/status/history")]
[ApiController]
public class AssetStatusHistoryController : ControllerBase
{
    private readonly IAssetStatusHistoryService _historyService;

    public AssetStatusHistoryController(IAssetStatusHistoryService historyService)
    {
        _historyService = historyService;
    }

    [HttpGet("entity/{entityId}")]
    public async Task<ActionResult<IEnumerable<AssetStatusHistory>>> GetEntityStatusHistory(
        string entityId,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        return Ok(await _historyService.GetEntityStatusHistoryAsync(entityId, fromDate, toDate));
    }

    [HttpGet("entity/{entityId}/paged")]
    public async Task<ActionResult<IEnumerable<AssetStatusHistory>>> GetEntityStatusHistoryPaged(
        string entityId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        return Ok(await _historyService.GetEntityStatusHistoryWithPaginationAsync(entityId, page, pageSize));
    }

    [HttpGet("entity/{entityId}/latest")]
    public async Task<ActionResult<AssetStatusHistory>> GetLatestEntityStatus(string entityId)
    {
        var status = await _historyService.GetLatestEntityStatusAsync(entityId);
        return status == null ? NotFound() : Ok(status);
    }

    [HttpGet("entity/{entityId}/summary")]
    public async Task<ActionResult<Dictionary<AssetStatus, int>>> GetEntityStatusSummary(
        string entityId,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        return Ok(await _historyService.GetEntityStatusSummaryAsync(entityId, fromDate, toDate));
    }

    [HttpGet("entity/{entityId}/count")]
    public async Task<ActionResult<int>> GetEntityStatusHistoryCount(string entityId)
    {
        return Ok(await _historyService.GetEntityStatusHistoryCountAsync(entityId));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<AssetStatusHistory>> GetEntityStatusHistoryById(int id)
    {
        var record = await _historyService.GetEntityStatusHistoryByIdAsync(id);
        return record == null ? NotFound() : Ok(record);
    }

    [HttpPost]
    public async Task<ActionResult<AssetStatusHistory>> CreateEntityStatusHistory(
        [FromHeader(Name = "X-Company-Id")] string companyId,
        AssetStatusHistory statusHistory)
    {
        if (string.IsNullOrEmpty(companyId)) return BadRequest("X-Company-Id header is required");
        statusHistory.CompanyId = companyId;
        try
        {
            var created = await _historyService.CreateEntityStatusHistoryAsync(statusHistory);
            return CreatedAtAction(nameof(GetEntityStatusHistoryById), new { id = created.Id }, created);
        }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateEntityStatusHistory(int id, AssetStatusHistory statusHistory)
    {
        if (id != statusHistory.Id) return BadRequest("ID mismatch");
        statusHistory.ModifiedBy = User?.Identity?.Name ?? "System";
        try
        {
            await _historyService.UpdateEntityStatusHistoryAsync(statusHistory);
            return NoContent();
        }
        catch (ArgumentException ex) { return NotFound(ex.Message); }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteEntityStatusHistory(int id)
    {
        return await _historyService.DeleteEntityStatusHistoryAsync(id) ? NoContent() : NotFound();
    }

    [HttpGet("range")]
    public async Task<ActionResult<IEnumerable<AssetStatusHistory>>> GetEntityStatusHistoryByDateRange(
        [FromHeader(Name = "X-Company-Id")] string companyId,
        [FromQuery] DateTime fromDate,
        [FromQuery] DateTime toDate)
    {
        if (string.IsNullOrEmpty(companyId)) return BadRequest("X-Company-Id header is required");
        return Ok(await _historyService.GetEntityStatusHistoryByDateRangeAsync(fromDate, toDate));
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<AssetStatusHistory>>> GetAllEntityStatusHistory(
        [FromHeader(Name = "X-Company-Id")] string companyId)
    {
        if (string.IsNullOrEmpty(companyId)) return BadRequest("X-Company-Id header is required");
        return Ok(await _historyService.GetAllEntityStatusHistoryAsync(companyId));
    }
}
