using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Application.Shared.Data;
using Application.Shared.Models.Data;
using Microsoft.EntityFrameworkCore;

namespace Application.Shared.Services.Data;

public interface ISavedQueryService
{
    Task<List<SavedQueryDto>> GetForDatasetAsync(string companyId, string datasetId, string userId);
    Task<SavedQueryDto> CreateAsync(string companyId, string datasetId, string userId, SaveSavedQueryRequest request);
    Task<SavedQueryDto?> UpdateAsync(string companyId, string id, string userId, bool isAdmin, SaveSavedQueryRequest request);
    Task<bool> DeleteAsync(string companyId, string id, string userId, bool isAdmin);
}

public class SavedQueryService : ISavedQueryService
{
    private readonly ApplicationDbContext _db;

    public SavedQueryService(ApplicationDbContext db) => _db = db;

    public async Task<List<SavedQueryDto>> GetForDatasetAsync(string companyId, string datasetId, string userId)
    {
        // A user sees their own queries plus any shared within the dataset (same company).
        var queries = await _db.SavedQuery
            .Where(q => q.CompanyId == companyId && q.DatasetId == datasetId && (q.IsShared || q.CreatedBy == userId))
            .OrderByDescending(q => q.ModifiedAt ?? q.CreatedAt)
            .AsNoTracking()
            .ToListAsync();

        return queries.Select(q => ToDto(q, userId, isAdmin: false)).ToList();
    }

    public async Task<SavedQueryDto> CreateAsync(string companyId, string datasetId, string userId, SaveSavedQueryRequest request)
    {
        var query = new SavedQuery
        {
            Id = Guid.NewGuid().ToString(),
            CompanyId = companyId,
            DatasetId = datasetId,
            Name = request.Name?.Trim() ?? string.Empty,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            QueryText = request.QueryText ?? string.Empty,
            IsShared = request.IsShared,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId,
        };

        _db.SavedQuery.Add(query);
        await _db.SaveChangesAsync();
        return ToDto(query, userId, isAdmin: false);
    }

    public async Task<SavedQueryDto?> UpdateAsync(string companyId, string id, string userId, bool isAdmin, SaveSavedQueryRequest request)
    {
        var query = await _db.SavedQuery.FirstOrDefaultAsync(q => q.CompanyId == companyId && q.Id == id);
        if (query == null) return null;
        if (!isAdmin && query.CreatedBy != userId) return null; // only creator or admin may edit

        query.Name = request.Name?.Trim() ?? query.Name;
        query.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        query.QueryText = request.QueryText ?? query.QueryText;
        query.IsShared = request.IsShared;
        query.ModifiedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return ToDto(query, userId, isAdmin);
    }

    public async Task<bool> DeleteAsync(string companyId, string id, string userId, bool isAdmin)
    {
        var query = await _db.SavedQuery.FirstOrDefaultAsync(q => q.CompanyId == companyId && q.Id == id);
        if (query == null) return false;
        if (!isAdmin && query.CreatedBy != userId) return false;

        _db.SavedQuery.Remove(query);
        await _db.SaveChangesAsync();
        return true;
    }

    private static SavedQueryDto ToDto(SavedQuery q, string userId, bool isAdmin) => new()
    {
        Id = q.Id,
        DatasetId = q.DatasetId,
        Name = q.Name,
        Description = q.Description,
        QueryText = q.QueryText,
        IsShared = q.IsShared,
        CreatedBy = q.CreatedBy,
        CreatedAt = q.CreatedAt,
        ModifiedAt = q.ModifiedAt,
        CanEdit = isAdmin || q.CreatedBy == userId,
    };
}
