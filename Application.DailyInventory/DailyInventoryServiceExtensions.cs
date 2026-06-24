using Application.DailyInventory.Configuration;
using Application.DailyInventory.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Application.DailyInventory;

public static class DailyInventoryServiceExtensions
{
    public static IServiceCollection AddDailyInventory(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ClickHouseSettings>(configuration.GetSection("DailyInventory"));
        services.AddScoped<IDailyInventoryService, DailyInventoryService>();
        return services;
    }
}
