using Application.Client;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Authorization;
using Application.Shared.Services;
using Application.Shared.Authorization;
using Application.Client.Authorization;


var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.Services.AddFluentUIComponents();

builder.Services.AddAuthorizationCore(options => options.AddFlowbytePolicies());
builder.Services.AddScoped<ICurrentCompanyAccessor, QueryStringCompanyAccessor>();
builder.Services.AddScoped<IAuthorizationHandler, ModuleAccessHandler>();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddSingleton<AuthenticationStateProvider, PersistentAuthenticationStateProvider>();


builder.Services.AddScoped(http => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddScoped<ClientAuthenticationDetail>();
builder.Services.AddScoped<StateContainer>();

// Status Module Client Services
builder.Services.AddScoped<Application.Client.Services.MonitoredAssetClientService>();
builder.Services.AddScoped<Application.Client.Services.IncidentClientService>();
builder.Services.AddScoped<Application.Client.Services.AssetStatusHistoryClientService>();
builder.Services.AddScoped<Application.Client.Services.ServerManagementClientService>();

await builder.Build().RunAsync();
