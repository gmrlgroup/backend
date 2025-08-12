using Application.Shared.Data;
using Application.Shared.Models.Data;
using Application.Shared.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Shared.Services.Data;

public class RealTimeDataService : IRealTimeDataService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<RealTimeDataService> _logger;

    public RealTimeDataService(
        ApplicationDbContext context,
        ILogger<RealTimeDataService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<SalesData?> CreateSalesDataAsync(SalesData salesData)
    {
        try
        {
            // salesData.CreatedBy = userId;
            // salesData.ModifiedBy = userId;
            salesData.ReceivedAt = DateTime.UtcNow;

            _context.SalesData.Add(salesData);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Sales data created with ID: {SalesDataId}", salesData.Id);

            // Broadcast the new sales data in real-time
            await BroadcastSalesDataAsync(salesData, salesData.CompanyId ?? "");

            return salesData;
        }
        catch (Exception ex)
        {
            // _logger.LogError(ex, "Error creating sales data for user: {UserId}", userId);
            throw;
        }
    }

    public async Task<List<SalesData>> GetSalesDataAsync(string companyId, string userId, int? fromHour = null, int? toHour = null)
    {
        var query = _context.SalesData
            .Where(sd => sd.CompanyId == companyId);

        if (fromHour.HasValue)
            query = query.Where(sd => sd.Hour >= fromHour.Value);

        if (toHour.HasValue)
            query = query.Where(sd => sd.Hour <= toHour.Value);

        return await query
            .OrderByDescending(sd => sd.Hour)
            .ToListAsync();
    }

    public async Task<List<SalesData>> GetSalesDataByStoreAsync(string companyId, string storeCode, string userId, int? fromHour = null, int? toHour = null)
    {
        var query = _context.SalesData
            .Where(sd => sd.CompanyId == companyId && sd.StoreCode == storeCode);

        if (fromHour.HasValue)
            query = query.Where(sd => sd.Hour >= fromHour.Value);

        if (toHour.HasValue)
            query = query.Where(sd => sd.Hour <= toHour.Value);

        return await query
            .OrderByDescending(sd => sd.Hour)
            .ToListAsync();
    }



    public async Task<List<SalesData>> GetSalesDataBySchemeAsync(string companyId, string scheme, string userId, int? fromHour = null, int? toHour = null)
    {
        var query = _context.SalesData
            .Where(sd => sd.CompanyId == companyId && sd.Scheme == scheme);

        if (fromHour.HasValue)
            query = query.Where(sd => sd.Hour >= fromHour.Value);

        if (toHour.HasValue)
            query = query.Where(sd => sd.Hour <= toHour.Value);

        return await query
            .OrderByDescending(sd => sd.Hour)
            .ToListAsync();
    }

    public async Task<SalesData?> GetSalesDataByIdAsync(string id, string companyId, string userId)
    {
        return await _context.SalesData
            .Where(sd => sd.Id == id && sd.CompanyId == companyId)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> MarkSalesDataAsProcessedAsync(string id, string userId)
    {
        try
        {
            var salesData = await _context.SalesData.FindAsync(id);
            if (salesData == null)
                return false;

            salesData.IsProcessed = true;
            salesData.ModifiedBy = userId;
            salesData.ModifiedOn = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking sales data as processed: {SalesDataId}", id);
            return false;
        }
    }

    public async Task<bool> DeleteSalesDataAsync(string id, string userId)
    {
        try
        {
            var salesData = await _context.SalesData.FindAsync(id);
            if (salesData == null)
                return false;

            _context.SalesData.Remove(salesData);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Sales data deleted: {SalesDataId} by user: {UserId}", id, userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting sales data: {SalesDataId}", id);
            return false;
        }
    }

    public async Task BroadcastSalesDataAsync(SalesData salesData, string companyId)
    {
        try
        {
            // Create notification message
            var notification = new Notification<SalesData>
            {
                Message = $"New sales data received from store {salesData.StoreCode}",
                Data = salesData
            };

            // Log the sales data reception for potential SignalR integration
            _logger.LogInformation("Sales data notification prepared for company: {CompanyId}, Store: {StoreCode}, Amount: {Amount}", 
                companyId, salesData.StoreCode, salesData.NetAmountAcy);
            
            await Task.CompletedTask; // Placeholder for async operation
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error preparing sales data notification for company: {CompanyId}", companyId);
        }
    }

    public async Task<List<SalesData>> GetUnprocessedSalesDataAsync(string companyId, string userId)
    {
        return await _context.SalesData
            .Where(sd => sd.CompanyId == companyId && !sd.IsProcessed)
            .OrderByDescending(sd => sd.ReceivedAt)
            .ToListAsync();
    }

    public async Task<int> GetSalesDataCountAsync(string companyId, string userId, int? fromHour = null, int? toHour = null)
    {
        var query = _context.SalesData
            .Where(sd => sd.CompanyId == companyId);

        if (fromHour.HasValue)
            query = query.Where(sd => sd.Hour >= fromHour.Value);

        if (toHour.HasValue)
            query = query.Where(sd => sd.Hour <= toHour.Value);

        return await query.CountAsync();
    }

    public async Task<decimal> GetTotalNetAmountAsync(string companyId, string userId, int? fromHour = null, int? toHour = null)
    {
        var query = _context.SalesData
            .Where(sd => sd.CompanyId == companyId);

        if (fromHour.HasValue)
            query = query.Where(sd => sd.Hour >= fromHour.Value);

        if (toHour.HasValue)
            query = query.Where(sd => sd.Hour <= toHour.Value);

        return await query.SumAsync(sd => sd.NetAmountAcy);
    }

    public async Task<int> GetTotalTransactionsAsync(string companyId, string userId, int? fromHour = null, int? toHour = null)
    {
        var query = _context.SalesData
            .Where(sd => sd.CompanyId == companyId);

        if (fromHour.HasValue)
            query = query.Where(sd => sd.Hour >= fromHour.Value);

        if (toHour.HasValue)
            query = query.Where(sd => sd.Hour <= toHour.Value);

        return await query.SumAsync(sd => sd.TotalTransactions);
    }

    public async Task<Dictionary<string, decimal>> GetSalesByStoreAsync(string companyId, string userId, int? fromHour = null, int? toHour = null)
    {
        var query = _context.SalesData
            .Where(sd => sd.CompanyId == companyId);

        if (fromHour.HasValue)
            query = query.Where(sd => sd.Hour >= fromHour.Value);

        if (toHour.HasValue)
            query = query.Where(sd => sd.Hour <= toHour.Value);

        return await query
            .GroupBy(sd => sd.StoreCode)
            .Select(g => new { StoreCode = g.Key, Total = g.Sum(sd => sd.NetAmountAcy) })
            .ToDictionaryAsync(x => x.StoreCode ?? "", x => x.Total);
    }

    public async Task<Dictionary<string, decimal>> GetSalesBySchemeAsync(string companyId, string userId, int? fromHour = null, int? toHour = null)
    {
        var query = _context.SalesData
            .Where(sd => sd.CompanyId == companyId);

        if (fromHour.HasValue)
            query = query.Where(sd => sd.Hour >= fromHour.Value);

        if (toHour.HasValue)
            query = query.Where(sd => sd.Hour <= toHour.Value);

        return await query
            .GroupBy(sd => sd.Scheme)
            .Select(g => new { Scheme = g.Key, Total = g.Sum(sd => sd.NetAmountAcy) })
            .ToDictionaryAsync(x => x.Scheme ?? "", x => x.Total);
    }
}
