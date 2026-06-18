using Application.Shared.Authorization;

namespace Application.Authorization;

/// <summary>
/// Server-side <see cref="ICurrentCompanyAccessor"/>: resolves the active company from the
/// <c>X-Company-Id</c> request header, falling back to the <c>c</c> query-string value.
/// </summary>
public sealed class HttpContextCompanyAccessor : ICurrentCompanyAccessor
{
    private const string CompanyHeader = "X-Company-Id";
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextCompanyAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? GetCompanyId()
    {
        var request = _httpContextAccessor.HttpContext?.Request;
        if (request is null)
            return null;

        var header = request.Headers[CompanyHeader].ToString();
        if (!string.IsNullOrWhiteSpace(header))
            return header;

        var query = request.Query["c"].ToString();
        return string.IsNullOrWhiteSpace(query) ? null : query;
    }
}
