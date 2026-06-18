namespace Application.Shared.Authorization;

/// <summary>
/// Resolves the active company for the current request/navigation. Implemented differently
/// per environment: the server reads the <c>X-Company-Id</c> header; the WASM client reads
/// the <c>c</c> query-string value.
/// </summary>
public interface ICurrentCompanyAccessor
{
    /// <summary>The active company id, or <c>null</c> if it cannot be determined.</summary>
    string? GetCompanyId();
}
