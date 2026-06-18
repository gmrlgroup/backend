using Application.Shared.Data;
using Application.Shared.Models;
using Application.Shared.Enums;
using Microsoft.EntityFrameworkCore;

namespace Application.Shared.Services;

public class IncidentService : IIncidentService
{
    private readonly StatusDbContext _context;

    public IncidentService(StatusDbContext context)
    {
        _context = context;
    }

    public async Task<List<Incident>> GetIncidentsAsync(string companyId)
    {
        return await _context.Incidents
            .Where(i => i.CompanyId == companyId && !i.IsDeleted)
            .Include(i => i.Entity)
            .Include(i => i.Updates!.OrderByDescending(u => u.PostedAt))
            .OrderByDescending(i => i.StartedAt)
            .ToListAsync();
    }

    public async Task<PagedResult<Incident>> GetIncidentsPagedAsync(string companyId, IncidentQueryParameters parameters)
    {
        var query = _context.Incidents
            .Where(i => i.CompanyId == companyId && !i.IsDeleted);

        if (parameters.ActiveOnly)
            query = query.Where(i => i.Status != IncidentStatus.Resolved);

        if (parameters.Severity.HasValue)
            query = query.Where(i => i.Severity == parameters.Severity.Value);

        if (parameters.Status.HasValue)
            query = query.Where(i => i.Status == parameters.Status.Value);

        if (!string.IsNullOrWhiteSpace(parameters.Search))
        {
            var search = parameters.Search.Trim().ToLower();
            query = query.Where(i =>
                i.Title.ToLower().Contains(search) ||
                i.Description.ToLower().Contains(search) ||
                (i.Entity != null && i.Entity.Name.ToLower().Contains(search)));
        }

        var totalCount = await query.CountAsync();

        query = ApplySort(query, parameters.SortBy, parameters.SortDir);

        var items = await query
            .Skip((parameters.Page - 1) * parameters.PageSize)
            .Take(parameters.PageSize)
            .Include(i => i.Entity)
            .ToListAsync();

        return new PagedResult<Incident>
        {
            Items = items,
            TotalCount = totalCount,
            Page = parameters.Page,
            PageSize = parameters.PageSize
        };
    }

    private static IQueryable<Incident> ApplySort(IQueryable<Incident> query, string? sortBy, string? sortDir)
    {
        var desc = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);

        return (sortBy?.ToLower()) switch
        {
            "title" => desc ? query.OrderByDescending(i => i.Title) : query.OrderBy(i => i.Title),
            "entity" => desc ? query.OrderByDescending(i => i.Entity!.Name) : query.OrderBy(i => i.Entity!.Name),
            "severity" => desc ? query.OrderByDescending(i => i.Severity) : query.OrderBy(i => i.Severity),
            "status" => desc ? query.OrderByDescending(i => i.Status) : query.OrderBy(i => i.Status),
            "resolved" => desc ? query.OrderByDescending(i => i.ResolvedAt) : query.OrderBy(i => i.ResolvedAt),
            _ => desc ? query.OrderByDescending(i => i.StartedAt) : query.OrderBy(i => i.StartedAt),
        };
    }

    public async Task<List<Incident>> GetIncidentsByEntityAsync(string entityId)
    {
        return await _context.Incidents
            .Where(i => i.EntityId == entityId && !i.IsDeleted)
            .Include(i => i.Entity)
            .Include(i => i.Updates!.OrderByDescending(u => u.PostedAt))
            .OrderByDescending(i => i.StartedAt)
            .ToListAsync();
    }

    public async Task<Incident?> GetIncidentAsync(string id)
    {
        return await _context.Incidents
            .Where(i => !i.IsDeleted)
            .Include(i => i.Entity)
            .Include(i => i.Updates!.OrderByDescending(u => u.PostedAt))
            .FirstOrDefaultAsync(i => i.Id == id);
    }

    public async Task<List<Incident>> GetActiveIncidentsAsync(string companyId)
    {
        return await _context.Incidents
            .Where(i => i.CompanyId == companyId && i.Status != IncidentStatus.Resolved && !i.IsDeleted)
            .Include(i => i.Entity)
            .Include(i => i.Updates!.OrderByDescending(u => u.PostedAt))
            .OrderByDescending(i => i.StartedAt)
            .ToListAsync();
    }

    public async Task<Incident> CreateIncidentAsync(Incident incident)
    {
        if (string.IsNullOrEmpty(incident.Id))
            incident.Id = Guid.NewGuid().ToString();

        incident.CreatedOn = DateTime.UtcNow;
        incident.ModifiedOn = DateTime.UtcNow;
        incident.StartedAt = DateTime.UtcNow;

        _context.Incidents.Add(incident);
        await _context.SaveChangesAsync();

        return incident;
    }

    public async Task<Incident> UpdateIncidentAsync(Incident incident)
    {
        incident.ModifiedOn = DateTime.UtcNow;

        _context.Incidents.Update(incident);
        await _context.SaveChangesAsync();

        return incident;
    }

    public async Task<Incident> UpdateIncidentStatusAsync(string incidentId, IncidentStatus status, string? message = null, string? updatedBy = null)
    {
        var incident = await GetIncidentAsync(incidentId)
            ?? throw new ArgumentException("Incident not found", nameof(incidentId));

        var previousStatus = incident.Status;
        incident.Status = status;
        incident.ModifiedOn = DateTime.UtcNow;

        if (status == IncidentStatus.Resolved && !incident.ResolvedAt.HasValue)
            incident.ResolvedAt = DateTime.UtcNow;

        if (!string.IsNullOrEmpty(message) || previousStatus != status)
        {
            var update = new IncidentUpdate
            {
                IncidentId = incidentId,
                Message = message ?? $"Status changed from {previousStatus} to {status}",
                StatusChange = status,
                Author = updatedBy,
                CompanyId = incident.CompanyId
            };

            await CreateIncidentUpdateAsync(update);
        }

        _context.Incidents.Update(incident);
        await _context.SaveChangesAsync();

        return incident;
    }

    public async Task<Incident> ResolveIncidentAsync(string incidentId, string resolutionDetails, string? resolvedBy = null)
    {
        var incident = await GetIncidentAsync(incidentId)
            ?? throw new ArgumentException("Incident not found", nameof(incidentId));

        incident.Status = IncidentStatus.Resolved;
        incident.ResolvedAt = DateTime.UtcNow;
        incident.ResolutionDetails = resolutionDetails;
        incident.ModifiedOn = DateTime.UtcNow;

        var update = new IncidentUpdate
        {
            IncidentId = incidentId,
            Message = $"Incident resolved: {resolutionDetails}",
            StatusChange = IncidentStatus.Resolved,
            Author = resolvedBy,
            CompanyId = incident.CompanyId
        };

        await CreateIncidentUpdateAsync(update);

        _context.Incidents.Update(incident);
        await _context.SaveChangesAsync();

        return incident;
    }

    public async Task<IncidentUpdate> CreateIncidentUpdateAsync(IncidentUpdate update)
    {
        if (string.IsNullOrEmpty(update.Id))
            update.Id = Guid.NewGuid().ToString();

        update.CreatedOn = DateTime.UtcNow;
        update.ModifiedOn = DateTime.UtcNow;
        update.PostedAt = DateTime.UtcNow;

        _context.IncidentUpdates.Add(update);
        await _context.SaveChangesAsync();

        return update;
    }

    public async Task<List<IncidentUpdate>> GetIncidentUpdatesAsync(string incidentId)
    {
        return await _context.IncidentUpdates
            .Where(u => u.IncidentId == incidentId)
            .OrderByDescending(u => u.PostedAt)
            .ToListAsync();
    }

    public async Task DeleteIncidentAsync(string id)
    {
        var incident = await _context.Incidents.FindAsync(id);
        if (incident != null)
        {
            incident.IsDeleted = true;
            incident.DeletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<int> GetActiveIncidentCountAsync(string companyId)
    {
        return await _context.Incidents
            .CountAsync(i => i.CompanyId == companyId && i.Status != IncidentStatus.Resolved && !i.IsDeleted);
    }

    public async Task<int> GetCriticalIncidentCountAsync(string companyId)
    {
        return await _context.Incidents
            .CountAsync(i => i.CompanyId == companyId &&
                            i.Status != IncidentStatus.Resolved &&
                            i.Severity == IncidentSeverity.Critical &&
                            !i.IsDeleted);
    }
}
