using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Shared.Authorization;
using Application.Shared.Models.Dashboards;
using Application.Dashboard.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Application.Controllers;

/// <summary>
/// Manages links between dashboard pages and ingested dataset tables. Driven from the dataset tables
/// page, so it follows the same header/role convention as <c>DatasetsController</c>
/// (X-Company-ID + UserId headers; VIEW_DATA to read, EDIT_DATA to change).
/// </summary>
[Route("api/dashboard-links")]
[ApiController]
[Authorize(Policy = PolicyNames.DatasetsAccess)]
public class DashboardLinksController : ControllerBase
{
    private readonly IDashboardLinkService _links;

    public DashboardLinksController(IDashboardLinkService links)
    {
        _links = links;
    }

    // GET: api/dashboard-links/dashboards — the dashboards a table can be connected to.
    [HttpGet("dashboards")]
    public ActionResult<IEnumerable<DashboardPageInfo>> GetDashboards() => Ok(DashboardPages.All);

    // GET: api/dashboard-links?datasetId=&tableName= — dashboards this table is currently linked to.
    [HttpGet]
    public async Task<ActionResult<IEnumerable<DashboardDataLink>>> GetForTable(
        [FromQuery] string datasetId, [FromQuery] string tableName, CancellationToken ct)
    {
        var (companyId, _, error) = ReadHeaders();
        if (error != null) return BadRequest(error);
        if (!User.HasCompanyRole(companyId, "VIEW_DATA")) return Forbid();
        if (string.IsNullOrWhiteSpace(datasetId) || string.IsNullOrWhiteSpace(tableName))
            return BadRequest("datasetId and tableName are required");

        return Ok(await _links.GetForTableAsync(companyId, datasetId, tableName, ct));
    }

    // POST: api/dashboard-links — connect a dashboard page to a table (upsert).
    [HttpPost]
    public async Task<ActionResult<DashboardDataLink>> Connect([FromBody] DashboardLinkRequest request, CancellationToken ct)
    {
        var (companyId, userId, error) = ReadHeaders();
        if (error != null) return BadRequest(error);
        if (!User.HasCompanyRole(companyId, "EDIT_DATA")) return Forbid();
        if (request == null || string.IsNullOrWhiteSpace(request.PageUrl) ||
            string.IsNullOrWhiteSpace(request.DatasetId) || string.IsNullOrWhiteSpace(request.TableName))
            return BadRequest("pageUrl, datasetId and tableName are required");

        if (!DashboardPages.All.Any(d => d.PageUrl == request.PageUrl))
            return BadRequest($"Unknown dashboard page '{request.PageUrl}'.");

        var link = await _links.SetAsync(companyId, request.PageUrl, request.DatasetId, request.TableName, userId, ct);
        return Ok(link);
    }

    // DELETE: api/dashboard-links?pageUrl= — disconnect a dashboard page.
    [HttpDelete]
    public async Task<IActionResult> Disconnect([FromQuery] string pageUrl, CancellationToken ct)
    {
        var (companyId, _, error) = ReadHeaders();
        if (error != null) return BadRequest(error);
        if (!User.HasCompanyRole(companyId, "EDIT_DATA")) return Forbid();
        if (string.IsNullOrWhiteSpace(pageUrl)) return BadRequest("pageUrl is required");

        return await _links.DeleteAsync(companyId, pageUrl, ct) ? NoContent() : NotFound();
    }

    private (string companyId, string userId, string? error) ReadHeaders()
    {
        var companyId = Request.Headers["X-Company-ID"].FirstOrDefault() ?? "";
        var userId = Request.Headers["UserId"].ToString();
        if (string.IsNullOrWhiteSpace(companyId)) return ("", "", "Company ID is required");
        if (string.IsNullOrWhiteSpace(userId)) return ("", "", "User ID is required in headers");
        return (companyId, userId, null);
    }
}
