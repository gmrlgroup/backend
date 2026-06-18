using Application.Shared.Authorization;
using Application.Shared.Services;
using Microsoft.AspNetCore.Components;

namespace Application.Client.Authorization;

/// <summary>
/// Client-side <see cref="ICurrentCompanyAccessor"/>: resolves the active company from the
/// <c>c</c> query-string value of the current URL, falling back to the selected company in
/// <see cref="StateContainer"/> (the same source <c>NavMenu</c> uses).
/// </summary>
public sealed class QueryStringCompanyAccessor : ICurrentCompanyAccessor
{
    private readonly NavigationManager _navigation;
    private readonly StateContainer _stateContainer;

    public QueryStringCompanyAccessor(NavigationManager navigation, StateContainer stateContainer)
    {
        _navigation = navigation;
        _stateContainer = stateContainer;
    }

    public string? GetCompanyId()
    {
        var fromQuery = ReadQueryValue(_navigation.Uri, "c");
        if (!string.IsNullOrWhiteSpace(fromQuery))
            return fromQuery;

        return _stateContainer.Company?.Id;
    }

    private static string? ReadQueryValue(string url, string key)
    {
        var queryIndex = url.IndexOf('?');
        if (queryIndex < 0 || queryIndex == url.Length - 1)
            return null;

        var query = url[(queryIndex + 1)..];
        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = pair.IndexOf('=');
            var name = eq < 0 ? pair : pair[..eq];
            if (string.Equals(name, key, StringComparison.OrdinalIgnoreCase))
            {
                var rawValue = eq < 0 ? string.Empty : pair[(eq + 1)..];
                return Uri.UnescapeDataString(rawValue);
            }
        }

        return null;
    }
}
