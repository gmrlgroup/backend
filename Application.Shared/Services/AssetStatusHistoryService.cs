using Application.Shared.Data;
using Application.Shared.Models;
using Application.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Shared.Services;

public class AssetStatusHistoryService : IAssetStatusHistoryService
{
    private readonly StatusDbContext _context;
    private readonly ILogger<AssetStatusHistoryService> _logger;

    public AssetStatusHistoryService(StatusDbContext context, ILogger<AssetStatusHistoryService> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<List<AssetStatusHistory>> GetEntityStatusHistoryAsync(string entityId, DateTime? fromDate = null, DateTime? toDate = null)
    {
        try
        {
            var query = _context.AssetStatusHistory
                .Where(h => h.EntityId == entityId && !h.IsDeleted);

            if (fromDate.HasValue) query = query.Where(h => h.CheckedAt >= fromDate.Value);
            if (toDate.HasValue) query = query.Where(h => h.CheckedAt <= toDate.Value);

            return await query.OrderByDescending(h => h.CheckedAt).ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting entity status history for entity {EntityId}", entityId);
            throw;
        }
    }

    public async Task<AssetStatusHistory?> GetEntityStatusHistoryByIdAsync(int id)
    {
        try
        {
            return await _context.AssetStatusHistory
                .Include(h => h.Entity)
                .FirstOrDefaultAsync(h => h.Id == id && !h.IsDeleted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting entity status history by ID {Id}", id);
            throw;
        }
    }

    public async Task<AssetStatusHistory?> GetLatestEntityStatusAsync(string entityId)
    {
        try
        {
            return await _context.AssetStatusHistory
                .Where(h => h.EntityId == entityId && !h.IsDeleted)
                .OrderByDescending(h => h.CheckedAt)
                .FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting latest entity status for entity {EntityId}", entityId);
            throw;
        }
    }

    public async Task<List<AssetStatusHistory>> GetEntityStatusHistoryByStatusAsync(string entityId, AssetStatus status)
    {
        try
        {
            return await _context.AssetStatusHistory
                .Where(h => h.EntityId == entityId && h.Status == status && !h.IsDeleted)
                .OrderByDescending(h => h.CheckedAt)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting entity status history by status {Status} for entity {EntityId}", status, entityId);
            throw;
        }
    }

    public async Task<AssetStatusHistory> CreateEntityStatusHistoryAsync(AssetStatusHistory statusHistory)
    {
        try
        {
            if (statusHistory == null) throw new ArgumentNullException(nameof(statusHistory));

            var entityExists = await _context.MonitoredAssets.AnyAsync(e => e.Id == statusHistory.EntityId && !e.IsDeleted);
            if (!entityExists)
                throw new ArgumentException($"Entity with ID '{statusHistory.EntityId}' not found", nameof(statusHistory));

            statusHistory.CreatedOn = DateTime.UtcNow;
            statusHistory.ModifiedOn = DateTime.UtcNow;
            statusHistory.CheckedAt = statusHistory.CheckedAt == default ? DateTime.UtcNow : statusHistory.CheckedAt;

            _context.AssetStatusHistory.Add(statusHistory);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created status history record {Id} for entity {EntityId}", statusHistory.Id, statusHistory.EntityId);

            return statusHistory;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating entity status history for entity {EntityId}", statusHistory?.EntityId);
            throw;
        }
    }

    public async Task<AssetStatusHistory> UpdateEntityStatusHistoryAsync(AssetStatusHistory statusHistory)
    {
        try
        {
            if (statusHistory == null) throw new ArgumentNullException(nameof(statusHistory));

            var existing = await _context.AssetStatusHistory
                .FirstOrDefaultAsync(h => h.Id == statusHistory.Id && !h.IsDeleted)
                ?? throw new ArgumentException($"Asset status history with ID '{statusHistory.Id}' not found", nameof(statusHistory));

            existing.Status = statusHistory.Status;
            existing.StatusMessage = statusHistory.StatusMessage;
            existing.ResponseTime = statusHistory.ResponseTime;
            existing.UptimePercentage = statusHistory.UptimePercentage;
            existing.CheckedAt = statusHistory.CheckedAt;
            existing.ModifiedOn = DateTime.UtcNow;
            existing.ModifiedBy = statusHistory.ModifiedBy;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Updated status history record {Id} for entity {EntityId}", statusHistory.Id, statusHistory.EntityId);

            return existing;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating entity status history {Id}", statusHistory?.Id);
            throw;
        }
    }

    public async Task<bool> DeleteEntityStatusHistoryAsync(int id)
    {
        try
        {
            var statusHistory = await _context.AssetStatusHistory
                .FirstOrDefaultAsync(h => h.Id == id && !h.IsDeleted);

            if (statusHistory == null)
            {
                _logger.LogWarning("Asset status history {Id} not found for deletion", id);
                return false;
            }

            statusHistory.IsDeleted = true;
            statusHistory.DeletedAt = DateTime.UtcNow;
            statusHistory.ModifiedOn = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Deleted asset status history record {Id}", id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting entity status history {Id}", id);
            throw;
        }
    }

    public async Task<List<AssetStatusHistory>> GetEntityStatusHistoryByDateRangeAsync(DateTime fromDate, DateTime toDate)
    {
        try
        {
            return await _context.AssetStatusHistory
                .Include(h => h.Entity)
                .Where(h => h.CheckedAt >= fromDate && h.CheckedAt <= toDate && !h.IsDeleted)
                .OrderByDescending(h => h.CheckedAt)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting entity status history by date range {FromDate} to {ToDate}", fromDate, toDate);
            throw;
        }
    }

    public async Task<List<AssetStatusHistory>> GetAllEntityStatusHistoryAsync(string companyId)
    {
        try
        {
            return await _context.AssetStatusHistory
                .Include(h => h.Entity)
                .Where(h => h.CompanyId == companyId && !h.IsDeleted)
                .OrderByDescending(h => h.CheckedAt)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all entity status history for company {CompanyId}", companyId);
            throw;
        }
    }

    public async Task<Dictionary<AssetStatus, int>> GetEntityStatusSummaryAsync(string entityId, DateTime? fromDate = null, DateTime? toDate = null)
    {
        try
        {
            var query = _context.AssetStatusHistory
                .Where(h => h.EntityId == entityId && !h.IsDeleted);

            if (fromDate.HasValue) query = query.Where(h => h.CheckedAt >= fromDate.Value);
            if (toDate.HasValue) query = query.Where(h => h.CheckedAt <= toDate.Value);

            return await query.GroupBy(h => h.Status).ToDictionaryAsync(g => g.Key, g => g.Count());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting entity status summary for entity {EntityId}", entityId);
            throw;
        }
    }

    public async Task<double> GetAverageResponseTimeAsync(string entityId, DateTime? fromDate = null, DateTime? toDate = null)
    {
        try
        {
            var query = _context.AssetStatusHistory
                .Where(h => h.EntityId == entityId && h.ResponseTime.HasValue && !h.IsDeleted);

            if (fromDate.HasValue) query = query.Where(h => h.CheckedAt >= fromDate.Value);
            if (toDate.HasValue) query = query.Where(h => h.CheckedAt <= toDate.Value);

            return await query.AnyAsync() ? await query.AverageAsync(h => h.ResponseTime!.Value) : 0.0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating average response time for entity {EntityId}", entityId);
            throw;
        }
    }

    public async Task<double> GetAverageUptimeAsync(string entityId, DateTime? fromDate = null, DateTime? toDate = null)
    {
        try
        {
            var query = _context.AssetStatusHistory
                .Where(h => h.EntityId == entityId && h.UptimePercentage.HasValue && !h.IsDeleted);

            if (fromDate.HasValue) query = query.Where(h => h.CheckedAt >= fromDate.Value);
            if (toDate.HasValue) query = query.Where(h => h.CheckedAt <= toDate.Value);

            return await query.AnyAsync() ? await query.AverageAsync(h => h.UptimePercentage!.Value) : 0.0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating average uptime for entity {EntityId}", entityId);
            throw;
        }
    }

    public async Task<List<AssetStatusHistory>> GetEntityStatusHistoryWithPaginationAsync(string entityId, int page, int pageSize)
    {
        try
        {
            return await _context.AssetStatusHistory
                .Where(h => h.EntityId == entityId && !h.IsDeleted)
                .OrderByDescending(h => h.CheckedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting paginated status history for entity {EntityId}, page {Page}", entityId, page);
            throw;
        }
    }

    public async Task<int> GetEntityStatusHistoryCountAsync(string entityId)
    {
        try
        {
            return await _context.AssetStatusHistory
                .CountAsync(h => h.EntityId == entityId && !h.IsDeleted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting entity status history count for entity {EntityId}", entityId);
            throw;
        }
    }

    public async Task<bool> EntityStatusHistoryExistsAsync(int id)
    {
        try
        {
            return await _context.AssetStatusHistory.AnyAsync(h => h.Id == id && !h.IsDeleted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if entity status history {Id} exists", id);
            throw;
        }
    }
}
