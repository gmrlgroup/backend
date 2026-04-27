using Application.Client;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Application.Shared.Services;


var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.Services.AddFluentUIComponents();

builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddSingleton<AuthenticationStateProvider, PersistentAuthenticationStateProvider>();


builder.Services.AddScoped(http => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddScoped<ClientAuthenticationDetail>();
builder.Services.AddScoped<StateContainer>();

// Status Module Client Services
builder.Services.AddScoped<Application.Client.Services.MonitoredAssetClientService>();
builder.Services.AddScoped<Application.Client.Services.IncidentClientService>();
builder.Services.AddScoped<Application.Client.Services.AssetStatusHistoryClientService>();

await builder.Build().RunAsync();
