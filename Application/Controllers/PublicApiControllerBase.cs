using Application.Authorization;
using Application.Shared.Models.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Application.Controllers;

/// <summary>
/// Base for the external, API-key-authenticated public API consumed by the chat app. The API key
/// (validated by the <c>ApiKey</c> scheme, header <c>X-Api-Key</c> or <c>Authorization: Bearer</c>)
/// carries the tenant (<see cref="ApiKey.CompanyId"/>); the acting user is supplied per request via the
/// <c>X-User-Id</c> header. Access is then evaluated for that (company, user) pair.
/// </summary>
[ApiController]
[Authorize(AuthenticationSchemes = ApiKeyAuthenticationDefaults.Scheme)]
public abstract class PublicApiControllerBase : ControllerBase
{
    // The chat app sends the acting user as "X-User-Id" — the long-standing contract and the form that
    // survives reverse proxies (an X- prefixed header). "Userid" is kept only as a fallback. Reading both
    // (X-User-Id first) avoids a header-name mismatch silently 400-ing every request.
    protected static readonly string[] UserHeaderNames = { "X-User-Id", "Userid" };

    /// <summary>The API key resolved by the authentication handler for this request.</summary>
    protected ApiKey? CurrentKey =>
        HttpContext.Items.TryGetValue(ApiKeyAuthenticationDefaults.ApiKeyItem, out var v) ? v as ApiKey : null;

    /// <summary>Resolves (companyId from the key, userId from the acting-user header). Returns false + an
    /// error result when the key is invalid or the user header is missing.</summary>
    protected bool TryGetContext(out string companyId, out string userId, out ActionResult? error)
    {
        companyId = CurrentKey?.CompanyId ?? string.Empty;

        userId = string.Empty;
        foreach (var name in UserHeaderNames)
        {
            var candidate = Request.Headers[name].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                userId = candidate.Trim();
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(companyId))
        {
            error = Unauthorized("Invalid or missing API key.");
            return false;
        }
        if (string.IsNullOrWhiteSpace(userId))
        {
            error = BadRequest($"Missing or empty user header (expected one of: {string.Join(", ", UserHeaderNames)}).");
            return false;
        }
        error = null;
        return true;
    }
}
