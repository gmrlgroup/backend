using Application.Shared.Data;
using Application.Shared.Models.Dashboards;
using Microsoft.EntityFrameworkCore;

namespace Application.Dashboard.Services;

public class DashboardLinkService : IDashboardLinkService
{
    private readonly ApplicationDbContext _context;

    public DashboardLinkService(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<DashboardDataLink?> GetAsync(string companyId, string pageUrl, CancellationToken ct = default)
        => _context.DashboardDataLink.AsNoTracking()
            .FirstOrDefaultAsync(l => l.CompanyId == companyId && l.PageUrl == pageUrl, ct);

    public async Task<List<DashboardDataLink>> GetForTableAsync(string companyId, string datasetId, string tableName, CancellationToken ct = default)
        => await _context.DashboardDataLink.AsNoTracking()
            .Where(l => l.CompanyId == companyId && l.DatasetId == datasetId && l.TableName == tableName)
            .ToListAsync(ct);

    public async Task<DashboardDataLink> SetAsync(string companyId, string pageUrl, string datasetId, string tableName, string? userId, CancellationToken ct = default)
    {
        var link = await _context.DashboardDataLink
            .FirstOrDefaultAsync(l => l.CompanyId == companyId && l.PageUrl == pageUrl, ct);

        if (link == null)
        {
            link = new DashboardDataLink
            {
                CompanyId = companyId,
                PageUrl = pageUrl,
                DatasetId = datasetId,
                TableName = tableName,
                CreatedBy = userId,
                CreatedAt = DateTime.UtcNow
            };
            _context.DashboardDataLink.Add(link);
        }
        else
        {
            link.DatasetId = datasetId;
            link.TableName = tableName;
        }

        await _context.SaveChangesAsync(ct);
        return link;
    }

    public async Task<bool> DeleteAsync(string companyId, string pageUrl, CancellationToken ct = default)
    {
        var link = await _context.DashboardDataLink
            .FirstOrDefaultAsync(l => l.CompanyId == companyId && l.PageUrl == pageUrl, ct);
        if (link == null) return false;

        _context.DashboardDataLink.Remove(link);
        await _context.SaveChangesAsync(ct);
        return true;
    }
}
