using Application.Dashboard.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Dashboard;

public static class DashboardServiceExtensions
{
    /// <summary>Registers the dashboard feature services (OOS dashboard + dashboard/table links).</summary>
    public static IServiceCollection AddDashboard(this IServiceCollection services)
    {
        services.AddScoped<IOosDashboardService, OosDashboardService>();
        services.AddScoped<IDashboardLinkService, DashboardLinkService>();
        return services;
    }
}
