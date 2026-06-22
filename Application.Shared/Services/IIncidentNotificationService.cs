using Application.Shared.Models;

namespace Application.Shared.Services;

public interface IIncidentNotificationService
{
    /// <summary>
    /// Notifies the audience of the incident's entity — and the audiences of every entity
    /// it transitively depends on (upstream) — that an incident has been opened.
    /// Never throws: email failures are logged and swallowed so incident creation is unaffected.
    /// </summary>
    Task NotifyIncidentOpenedAsync(Incident incident, CancellationToken ct = default);
}
