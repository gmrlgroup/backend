using Microsoft.AspNetCore.Components.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Application.Shared.Services;

public class ClientAuthenticationDetail
{

    private readonly AuthenticationStateProvider _authenticationStateProvider;


    public ClientAuthenticationDetail(AuthenticationStateProvider authenticationStateProvider)
    {
        _authenticationStateProvider = authenticationStateProvider;
    }


    public async Task<bool> UserIsAuthenticated()
    {
        var authState = await _authenticationStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;

        if(user.Identity.IsAuthenticated)
        {
            return true;
        }

        return false;

    }

    public async Task<string> GetUserId()
    {
        var authState = await _authenticationStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;

        if (user.Identity is not null && user.Identity.IsAuthenticated)
        {
            return user.FindFirst(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
        }

        return null;


    }
}
