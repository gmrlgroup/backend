using Application.Shared.Data;
using Application.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Application.Shared.Services;

public class PowerBiConnectionService : IPowerBiConnectionService
{
    private readonly StatusDbContext _context;
    private readonly ICredentialProtector _protector;

    public PowerBiConnectionService(StatusDbContext context, ICredentialProtector protector)
    {
        _context = context;
        _protector = protector;
    }

    public async Task<List<PowerBiConnectionDto>> GetConnectionsAsync(string companyId)
    {
        var connections = await _context.PowerBiConnections
            .Where(c => c.CompanyId == companyId)
            .OrderBy(c => c.Name)
            .ToListAsync();

        return connections.Select(ToDto).ToList();
    }

    public async Task<PowerBiConnectionDto> CreateAsync(string companyId, PowerBiConnectionRequest request, string? createdBy)
    {
        var connection = new PowerBiConnection
        {
            Id = Guid.NewGuid().ToString(),
            CompanyId = companyId,
            Name = request.Name,
            TenantId = request.TenantId,
            ClientId = request.ClientId,
            ClientSecretEncrypted = string.IsNullOrEmpty(request.ClientSecret) ? null : _protector.Encrypt(request.ClientSecret),
            IsActive = request.IsActive,
            CreatedBy = createdBy,
            CreatedOn = DateTime.UtcNow,
            ModifiedOn = DateTime.UtcNow
        };

        _context.PowerBiConnections.Add(connection);
        await _context.SaveChangesAsync();

        return ToDto(connection);
    }

    public async Task<PowerBiConnectionDto?> UpdateAsync(string connectionId, string companyId, PowerBiConnectionRequest request, string? modifiedBy)
    {
        var connection = await _context.PowerBiConnections
            .FirstOrDefaultAsync(c => c.Id == connectionId && c.CompanyId == companyId);
        if (connection == null) return null;

        connection.Name = request.Name;
        connection.TenantId = request.TenantId;
        connection.ClientId = request.ClientId;
        connection.IsActive = request.IsActive;
        connection.ModifiedBy = modifiedBy;
        connection.ModifiedOn = DateTime.UtcNow;

        // Only replace the secret when a new one is supplied; blank keeps the existing secret.
        if (!string.IsNullOrEmpty(request.ClientSecret))
            connection.ClientSecretEncrypted = _protector.Encrypt(request.ClientSecret);

        await _context.SaveChangesAsync();

        return ToDto(connection);
    }

    public async Task<bool> DeleteAsync(string connectionId, string companyId)
    {
        var connection = await _context.PowerBiConnections
            .FirstOrDefaultAsync(c => c.Id == connectionId && c.CompanyId == companyId);
        if (connection == null) return false;

        // Block deletion while datasets still reference it (no cascade in this DB).
        var inUse = await _context.PowerBiDatasetLinks
            .AnyAsync(l => l.PowerBiConnectionId == connectionId && l.CompanyId == companyId);
        if (inUse)
            throw new InvalidOperationException("This connection is still linked to one or more datasets.");

        _context.PowerBiConnections.Remove(connection);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<PowerBiConnection?> GetForExecutionAsync(string connectionId, string companyId)
    {
        var connection = await _context.PowerBiConnections
            .FirstOrDefaultAsync(c => c.Id == connectionId && c.CompanyId == companyId);
        if (connection == null) return null;

        if (!string.IsNullOrEmpty(connection.ClientSecretEncrypted))
            connection.ClientSecretEncrypted = _protector.Decrypt(connection.ClientSecretEncrypted);

        return connection;
    }

    private static PowerBiConnectionDto ToDto(PowerBiConnection c) => new()
    {
        Id = c.Id,
        Name = c.Name,
        TenantId = c.TenantId,
        ClientId = c.ClientId,
        HasSecret = !string.IsNullOrEmpty(c.ClientSecretEncrypted),
        IsActive = c.IsActive,
        CreatedOn = c.CreatedOn,
        ModifiedOn = c.ModifiedOn
    };
}
