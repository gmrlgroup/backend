using Application.Client;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace Application.Components.Account
{
    // This is a server-side AuthenticationStateProvider that uses PersistentComponentState to flow the
    // authentication state to the client which is then fixed for the lifetime of the WebAssembly application.
    internal sealed class PersistingServerAuthenticationStateProvider : ServerAuthenticationStateProvider, IDisposable
    {
        private readonly PersistentComponentState state;
        private readonly IdentityOptions options;

        private readonly PersistingComponentStateSubscription subscription;

        private Task<AuthenticationState>? authenticationStateTask;

        public PersistingServerAuthenticationStateProvider(
            PersistentComponentState persistentComponentState,
            IOptions<IdentityOptions> optionsAccessor)
        {
            state = persistentComponentState;
            options = optionsAccessor.Value;

            AuthenticationStateChanged += OnAuthenticationStateChanged;
            subscription = state.RegisterOnPersisting(OnPersistingAsync, RenderMode.InteractiveWebAssembly);
        }

        private void OnAuthenticationStateChanged(Task<AuthenticationState> task)
        {
            authenticationStateTask = task;
        }

        private async Task OnPersistingAsync()
        {
            if (authenticationStateTask is null)
            {
                throw new UnreachableException($"Authentication state not set in {nameof(OnPersistingAsync)}().");
            }

            var authenticationState = await authenticationStateTask;
            var principal = authenticationState.User;

            if (principal.Identity?.IsAuthenticated == true)
            {
                var userId = principal.FindFirst(options.ClaimsIdentity.UserIdClaimType)?.Value;
                var email = principal.FindFirst(options.ClaimsIdentity.EmailClaimType)?.Value;

                // Retrieve roles using the claim type the authenticated identity actually uses for
                // roles. The OIDC handler sets RoleClaimType = "role" (see Program.cs), which differs
                // from IdentityOptions.ClaimsIdentity.RoleClaimType (ClaimTypes.Role). Reading the
                // latter here returned NO roles, so the WASM client never received the company-prefixed
                // roles (e.g. GMRL_ADMIN) and every client-side authorization check denied access.
                // Union the identity's own RoleClaimType with the well-known role claim types so the
                // client receives exactly the roles the server recognizes, regardless of provider.
                var roleClaimTypes = new HashSet<string>(StringComparer.Ordinal)
                {
                    (principal.Identity as System.Security.Claims.ClaimsIdentity)?.RoleClaimType
                        ?? options.ClaimsIdentity.RoleClaimType,
                    options.ClaimsIdentity.RoleClaimType,
                    System.Security.Claims.ClaimTypes.Role,
                    "role",
                    "roles",
                };

                var roles = principal.Claims
                                     .Where(claim => roleClaimTypes.Contains(claim.Type))
                                     .Select(claim => claim.Value)
                                     .Distinct(StringComparer.Ordinal)
                                     .ToList();


                if (userId != null && email != null)
                {
                    state.PersistAsJson(nameof(UserInfo), new UserInfo
                    {
                        UserId = userId,
                        Email = email,
                        Roles = roles

                    });
                }
            }
        }

        public void Dispose()
        {
            subscription.Dispose();
            AuthenticationStateChanged -= OnAuthenticationStateChanged;
        }
    }
}
