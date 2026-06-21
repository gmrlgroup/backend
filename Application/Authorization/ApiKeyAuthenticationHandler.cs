using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Application.Shared.Models.Data;
using Application.Shared.Services.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Application.Authorization;

public static class ApiKeyAuthenticationDefaults
{
    public const string Scheme = "ApiKey";
    public const string HeaderName = "X-Api-Key";
    // HttpContext.Items slot where the resolved ApiKey is stashed for per-request scope checks.
    public const string ApiKeyItem = "ApiKeyEntity";
}

/// <summary>
/// Authenticates external callers from an <c>X-Api-Key</c> header (or <c>Authorization: Bearer</c>).
/// On success the resolved <see cref="ApiKey"/> is placed in <c>HttpContext.Items</c> so controllers
/// can enforce per-dataset/table/operation scope, and the company id is surfaced as a claim.
/// </summary>
public class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IApiKeyService _apiKeyService;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IApiKeyService apiKeyService) : base(options, logger, encoder)
    {
        _apiKeyService = apiKeyService;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var raw = ExtractKey();
        if (string.IsNullOrWhiteSpace(raw))
            return AuthenticateResult.NoResult(); // no key presented — [Authorize] yields a 401

        var key = await _apiKeyService.ValidateAsync(raw);
        if (key == null)
            return AuthenticateResult.Fail("Invalid, revoked, or expired API key.");

        Context.Items[ApiKeyAuthenticationDefaults.ApiKeyItem] = key;

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, key.Id),
            new Claim("api_key_id", key.Id),
            new Claim("company_id", key.CompanyId),
            new Claim(ClaimTypes.Name, key.Name),
        };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
        return AuthenticateResult.Success(ticket);
    }

    private string? ExtractKey()
    {
        if (Request.Headers.TryGetValue(ApiKeyAuthenticationDefaults.HeaderName, out var header))
        {
            var value = header.ToString();
            if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
        }

        var auth = Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(auth) && auth.StartsWith("Bearer ", System.StringComparison.OrdinalIgnoreCase))
            return auth.Substring("Bearer ".Length).Trim();

        return null;
    }
}
