using Application.Shared.Data;
using Application.Shared.Models;
using Application.Shared.Enums;
using Microsoft.EntityFrameworkCore;

namespace Application.Shared.Services;

public class MonitoredAssetService : IMonitoredAssetService
{
    private readonly StatusDbContext _context;
    private readonly IIncidentService _incidentService;

    public MonitoredAssetService(StatusDbContext context, IIncidentService incidentService)
    {
        _context = context;
        _incidentService = incidentService;
    }

    public async Task<List<MonitoredAsset>> GetEntitiesAsync(string companyId)
    {
        return await _context.MonitoredAssets
            .Where(e => e.CompanyId == companyId && !e.IsDeleted)
            .Include(e => e.Dependencies)
            .Include(e => e.DependentOn)
            .Include(e => e.Jobs)
            .Include(e => e.StatusHistory.OrderByDescending(sh => sh.CheckedAt).Take(1))
            .OrderBy(e => e.Name)
            .ToListAsync();
    }

    public async Task<MonitoredAsset?> GetEntityAsync(string id)
    {
        return await _context.MonitoredAssets
            .Where(e => !e.IsDeleted)
            .Include(e => e.Dependencies)
            .Include(e => e.DependentOn)
            .Include(e => e.Jobs)
            .Include(e => e.StatusHistory.OrderByDescending(sh => sh.CheckedAt))
            .FirstOrDefaultAsync(e => e.Id == id);
    }

    public async Task<MonitoredAsset> CreateEntityAsync(MonitoredAsset entity)
    {
        if (string.IsNullOrEmpty(entity.Id))
            entity.Id = Guid.NewGuid().ToString();

        entity.CreatedOn = DateTime.UtcNow;
        entity.ModifiedOn = DateTime.UtcNow;

        _context.MonitoredAssets.Add(entity);
        await _context.SaveChangesAsync();

        return entity;
    }

    public async Task<MonitoredAsset> UpdateEntityAsync(MonitoredAsset entity)
    {
        entity.ModifiedOn = DateTime.UtcNow;

        _context.MonitoredAssets.Update(entity);
        await _context.SaveChangesAsync();

        return entity;
    }

    public async Task<bool> DeleteEntityAsync(string id)
    {
        var entity = await _context.MonitoredAssets.FindAsync(id);
        if (entity == null) return false;

        entity.IsDeleted = true;
        entity.DeletedAt = DateTime.UtcNow;
        entity.ModifiedOn = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<MonitoredAsset>> GetEntitiesByTypeAsync(string companyId, AssetType entityType)
    {
        return await _context.MonitoredAssets
            .Where(e => e.CompanyId == companyId && e.EntityType == entityType && !e.IsDeleted)
            .OrderBy(e => e.Name)
            .ToListAsync();
    }

    public async Task<List<MonitoredAsset>> GetCriticalEntitiesAsync(string companyId)
    {
        return await _context.MonitoredAssets
            .Where(e => e.CompanyId == companyId && e.IsCritical && !e.IsDeleted)
            .OrderBy(e => e.Name)
            .ToListAsync();
    }

    public async Task<List<MonitoredAsset>> GetActiveEntitiesAsync(string companyId)
    {
        return await _context.MonitoredAssets
            .Where(e => e.CompanyId == companyId && e.IsActive && !e.IsDeleted)
            .OrderBy(e => e.Name)
            .ToListAsync();
    }

    public async Task<List<MonitoredAsset>> GetEntitiesWithCurrentStatusAsync(string companyId, AssetStatus status)
    {
        return await _context.MonitoredAssets
            .Where(e => e.CompanyId == companyId && !e.IsDeleted &&
                       e.StatusHistory.OrderByDescending(sh => sh.CheckedAt).FirstOrDefault()!.Status == status)
            .OrderBy(e => e.Name)
            .ToListAsync();
    }

    public async Task<AssetStatusHistory> AddEntityStatusAsync(string entityId, AssetStatus status, string? statusMessage = null, double? responseTime = null, double? uptimePercentage = null)
    {
        var statusHistory = new AssetStatusHistory
        {
            EntityId = entityId,
            Status = status,
            StatusMessage = statusMessage,
            ResponseTime = responseTime != null ? Math.Round(responseTime.Value, 2) : null,
            UptimePercentage = uptimePercentage != null ? Math.Round(uptimePercentage.Value, 2) : null,
            CheckedAt = DateTime.UtcNow
        };

        _context.AssetStatusHistory.Add(statusHistory);
        await _context.SaveChangesAsync();

        return statusHistory;
    }

    public async Task<AssetStatusHistory> UpdateEntityStatusWithIncidentHandlingAsync(string entityId, AssetStatus newStatus, string statusMessage, AssetStatus previousStatus, string companyId, string updatedBy)
    {
        var statusHistory = await AddEntityStatusAsync(entityId, newStatus, statusMessage);

        try
        {
            var entity = await GetEntityAsync(entityId);
            if (entity == null)
                throw new ArgumentException("Entity not found", nameof(entityId));

            if (previousStatus == AssetStatus.Online && newStatus != AssetStatus.Online)
            {
                await CreateIncidentForStatusChange(entity, newStatus, statusMessage, companyId, updatedBy);
            }
            else if (previousStatus != AssetStatus.Online && newStatus == AssetStatus.Online)
            {
                await ResolveIncidentsForEntity(entityId, statusMessage, companyId, updatedBy);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling incidents for entity status change: {ex.Message}");
        }

        return statusHistory;
    }

    private async Task CreateIncidentForStatusChange(MonitoredAsset entity, AssetStatus newStatus, string statusMessage, string companyId, string createdBy)
    {
        var severityMapping = newStatus switch
        {
            AssetStatus.Error => IncidentSeverity.Critical,
            AssetStatus.Offline => entity.IsCritical ? IncidentSeverity.High : IncidentSeverity.Medium,
            AssetStatus.Degraded => IncidentSeverity.Medium,
            AssetStatus.Maintenance => IncidentSeverity.Low,
            _ => IncidentSeverity.Medium
        };

        var incident = new Incident
        {
            EntityId = entity.Id,
            Title = $"{entity.Name} - Status Changed to {newStatus}",
            Description = $"Entity '{entity.Name}' status has changed from Online to {newStatus}.\n\nDetails: {statusMessage}",
            Severity = severityMapping,
            Status = IncidentStatus.Open,
            ReportedBy = createdBy,
            CompanyId = companyId,
            ImpactDescription = entity.IsCritical ? "Critical entity affected - high impact expected" : "Service may be impacted"
        };

        await _incidentService.CreateIncidentAsync(incident);
    }

    private async Task ResolveIncidentsForEntity(string entityId, string resolutionMessage, string companyId, string resolvedBy)
    {
        var openIncidents = await _incidentService.GetIncidentsByEntityAsync(entityId);
        var activeIncidents = openIncidents.Where(i => i.Status != IncidentStatus.Resolved && i.CompanyId == companyId);

        foreach (var incident in activeIncidents)
        {
            try
            {
                await _incidentService.ResolveIncidentAsync(incident.Id, $"Entity status restored to Online. {resolutionMessage}", resolvedBy);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error resolving incident {incident.Id}: {ex.Message}");
            }
        }
    }

    public async Task<AssetStatusHistory?> GetLatestEntityStatusAsync(string entityId)
    {
        return await _context.AssetStatusHistory
            .Where(sh => sh.EntityId == entityId && !sh.IsDeleted)
            .OrderByDescending(sh => sh.CheckedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<List<AssetStatusHistory>> GetEntityStatusHistoryAsync(string entityId, DateTime? fromDate = null, DateTime? toDate = null)
    {
        var query = _context.AssetStatusHistory
            .Where(sh => sh.EntityId == entityId && !sh.IsDeleted);

        if (fromDate.HasValue) query = query.Where(sh => sh.CheckedAt >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(sh => sh.CheckedAt <= toDate.Value);

        return await query.OrderByDescending(sh => sh.CheckedAt).ToListAsync();
    }

    public async Task<bool> UpdateEntityUptimeAsync(string id, double uptimePercentage)
    {
        var entity = await _context.MonitoredAssets.FindAsync(id);
        if (entity == null) return false;

        await AddEntityStatusAsync(id, AssetStatus.Unknown, "Uptime updated", null, uptimePercentage);
        return true;
    }

    public async Task<bool> UpdateEntityResponseTimeAsync(string id, double responseTime)
    {
        var entity = await _context.MonitoredAssets.FindAsync(id);
        if (entity == null) return false;

        await AddEntityStatusAsync(id, AssetStatus.Unknown, "Response time updated", responseTime);
        return true;
    }

    public async Task<bool> PingEntityAsync(string id)
    {
        var entity = await _context.MonitoredAssets.FindAsync(id);
        if (entity == null || string.IsNullOrEmpty(entity.Url)) return false;

        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var response = await httpClient.GetAsync(entity.Url);
            stopwatch.Stop();

            var status = response.IsSuccessStatusCode ? AssetStatus.Online : AssetStatus.Error;
            var statusMessage = $"HTTP {(int)response.StatusCode} - {response.ReasonPhrase}";

            await AddEntityStatusAsync(id, status, statusMessage, stopwatch.Elapsed.TotalMilliseconds);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            await AddEntityStatusAsync(id, AssetStatus.Offline, ex.Message);
            return false;
        }
    }

    public async Task<List<MonitoredAsset>> SearchEntitiesAsync(string companyId, string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return await GetEntitiesAsync(companyId);

        var lower = searchTerm.ToLower();

        return await _context.MonitoredAssets
            .Where(e => e.CompanyId == companyId && !e.IsDeleted &&
                       (e.Name.ToLower().Contains(lower) ||
                        (e.Description != null && e.Description.ToLower().Contains(lower)) ||
                        (e.Owner != null && e.Owner.ToLower().Contains(lower)) ||
                        (e.Location != null && e.Location.ToLower().Contains(lower))))
            .OrderBy(e => e.Name)
            .ToListAsync();
    }

    public async Task<Dictionary<AssetStatus, int>> GetEntityStatusSummaryAsync(string companyId)
    {
        var entities = await _context.MonitoredAssets
            .Where(e => e.CompanyId == companyId && !e.IsDeleted)
            .Include(e => e.StatusHistory)
            .ToListAsync();

        return entities
            .Select(e => e.StatusHistory.OrderByDescending(sh => sh.CheckedAt).FirstOrDefault()?.Status ?? AssetStatus.Unknown)
            .GroupBy(s => s)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    public async Task<List<MonitoredAsset>> GetEntitiesDueForCheckAsync()
    {
        var checkThreshold = DateTime.UtcNow.AddMinutes(-15);

        return await _context.MonitoredAssets
            .Where(e => e.IsActive && !e.IsDeleted)
            .Include(e => e.StatusHistory)
            .ToListAsync()
            .ContinueWith(task => task.Result
                .Where(e => e.StatusHistory.OrderByDescending(sh => sh.CheckedAt).FirstOrDefault() == null ||
                           (e.StatusHistory.OrderByDescending(sh => sh.CheckedAt).FirstOrDefault()!.CheckedAt ?? DateTime.MinValue) < checkThreshold)
                .OrderBy(e => e.StatusHistory.OrderByDescending(sh => sh.CheckedAt).FirstOrDefault()?.CheckedAt ?? DateTime.MinValue)
                .ToList());
    }

    public async Task<bool> EntityExistsAsync(string id)
    {
        return await _context.MonitoredAssets.AnyAsync(e => e.Id == id && !e.IsDeleted);
    }

    public async Task<List<MonitoredAsset>> GetEntitiesWithLatestStatusAsync(string companyId)
    {
        return await _context.MonitoredAssets
            .Where(e => e.CompanyId == companyId && !e.IsDeleted)
            .Include(e => e.StatusHistory.OrderByDescending(sh => sh.CheckedAt).Take(1))
            .OrderBy(e => e.Name)
            .ToListAsync();
    }

    public async Task<AssetDependency> CreateEntityDependencyAsync(AssetDependency dependency)
    {
        dependency.Id = Guid.NewGuid().ToString();
        dependency.CreatedOn = DateTime.UtcNow;
        dependency.ModifiedOn = DateTime.UtcNow;

        _context.AssetDependencies.Add(dependency);
        await _context.SaveChangesAsync();

        return await _context.AssetDependencies
            .Include(d => d.Entity)
            .Include(d => d.DependsOnEntity)
            .FirstOrDefaultAsync(d => d.Id == dependency.Id) ?? dependency;
    }

    public async Task<AssetDependency> UpdateEntityDependencyAsync(AssetDependency dependency)
    {
        var existing = await _context.AssetDependencies.FindAsync(dependency.Id)
            ?? throw new InvalidOperationException("Dependency not found");

        existing.DependsOnEntityId = dependency.DependsOnEntityId;
        existing.Description = dependency.Description;
        existing.IsActive = dependency.IsActive;
        existing.IsCritical = dependency.IsCritical;
        existing.DependencyType = dependency.DependencyType;
        existing.Order = dependency.Order;
        existing.ModifiedOn = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return await _context.AssetDependencies
            .Include(d => d.Entity)
            .Include(d => d.DependsOnEntity)
            .FirstOrDefaultAsync(d => d.Id == dependency.Id) ?? existing;
    }

    public async Task<bool> DeleteEntityDependencyAsync(string dependencyId)
    {
        var dependency = await _context.AssetDependencies.FindAsync(dependencyId);
        if (dependency == null) return false;

        _context.AssetDependencies.Remove(dependency);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<AssetDependency>> GetEntityDependenciesAsync(string entityId)
    {
        return await _context.AssetDependencies
            .Where(d => d.EntityId == entityId)
            .Include(d => d.DependsOnEntity)
            .OrderBy(d => d.Order)
            .ToListAsync();
    }

    public async Task<List<AssetDependency>> GetEntityDependentsAsync(string entityId)
    {
        return await _context.AssetDependencies
            .Where(d => d.DependsOnEntityId == entityId)
            .Include(d => d.Entity)
            .OrderBy(d => d.Order)
            .ToListAsync();
    }

    public async Task<AssetDependencyTree> GetEntityDependencyTreeAsync(string entityId)
    {
        var entity = await GetEntityAsync(entityId)
            ?? throw new ArgumentException("Entity not found", nameof(entityId));

        var latestStatus = await GetLatestEntityStatusAsync(entityId);

        var tree = new AssetDependencyTree
        {
            EntityId = entity.Id,
            EntityName = entity.Name,
            EntityType = entity.EntityType,
            CurrentStatus = latestStatus?.Status ?? AssetStatus.Unknown
        };

        tree.Dependencies = await BuildDependencyNodes(entityId, true, new HashSet<string>(), 0);
        tree.Dependents = await BuildDependencyNodes(entityId, false, new HashSet<string>(), 0);

        return tree;
    }

    private async Task<List<AssetDependencyTreeNode>> BuildDependencyNodes(string entityId, bool isDependency, HashSet<string> visited, int level)
    {
        if (visited.Contains(entityId)) return new List<AssetDependencyTreeNode>();
        visited.Add(entityId);

        var nodes = new List<AssetDependencyTreeNode>();
        List<AssetDependency> dependencies;

        if (isDependency)
        {
            dependencies = await _context.AssetDependencies
                .Where(d => d.EntityId == entityId)
                .Include(d => d.DependsOnEntity)
                .OrderBy(d => d.Order)
                .ToListAsync();
        }
        else
        {
            dependencies = await _context.AssetDependencies
                .Where(d => d.DependsOnEntityId == entityId)
                .Include(d => d.Entity)
                .OrderBy(d => d.Order)
                .ToListAsync();
        }

        foreach (var dep in dependencies)
        {
            var target = isDependency ? dep.DependsOnEntity : dep.Entity;
            if (target == null) continue;

            var targetStatus = await GetLatestEntityStatusAsync(target.Id);

            var node = new AssetDependencyTreeNode
            {
                EntityId = target.Id,
                EntityName = target.Name,
                EntityType = target.EntityType,
                CurrentStatus = targetStatus?.Status ?? AssetStatus.Unknown,
                IsCritical = dep.IsCritical,
                IsActive = dep.IsActive,
                Description = dep.Description,
                Order = dep.Order,
                Level = level
            };

            if (level < 5)
            {
                var newVisited = new HashSet<string>(visited);
                node.Children = await BuildDependencyNodes(target.Id, isDependency, newVisited, level + 1);
            }

            nodes.Add(node);
        }

        visited.Remove(entityId);
        return nodes;
    }
}
