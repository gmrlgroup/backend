using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Application.Shared.Services.Data;
using Application.Attributes;
using System.Security.Claims;

namespace Application.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class SalesDashboardController : ControllerBase
{
    private readonly ILogger<SalesDashboardController> _logger;
    private readonly ISalesDashboardService _salesDashboardService;

    public SalesDashboardController(
        ILogger<SalesDashboardController> logger,
        ISalesDashboardService salesDashboardService)
    {
        _logger = logger;
        _salesDashboardService = salesDashboardService;
    }

    [HttpGet("kpi")]
    [RequireCompanyHeader]
    public async Task<ActionResult<SalesKpiData>> GetKpiData()
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";
            var companyId = Request.Headers["X-Company-ID"].FirstOrDefault() ?? "unknown";

            var kpiData = await _salesDashboardService.GetKpiDataAsync(companyId, userId);
            return Ok(kpiData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving KPI data for user {UserId}", 
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("banner-kpi")]
    [RequireCompanyHeader]
    public async Task<ActionResult<List<SalesBannerKpi>>> GetBannerKpiData()
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";
            var companyId = Request.Headers["X-Company-ID"].FirstOrDefault() ?? "unknown";

            var bannerKpiData = await _salesDashboardService.GetBannerKpiDataAsync(companyId, userId);
            return Ok(bannerKpiData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving banner KPI data for user {UserId}", 
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("data")]
    [RequireCompanyHeader]
    public async Task<ActionResult<List<SalesDashboardData>>> GetDashboardData()
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";
            var companyId = Request.Headers["X-Company-ID"].FirstOrDefault() ?? "unknown";

            var dashboardData = await _salesDashboardService.GetDashboardDataAsync(companyId, userId);
            return Ok(dashboardData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving dashboard data for user {UserId}", 
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown");
            return StatusCode(500, "Internal server error");
        }
    }
}
