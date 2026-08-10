using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Shared.Authorization;
using Application.Shared.Data;
using Application.Shared.Models.Data;
using Microsoft.EntityFrameworkCore;

namespace Application.Shared.Services.Data;

/// <summary>The outcome of checking an acting user's company membership and roles.</summary>
/// <param name="Allowed">True when the user may proceed.</param>
/// <param name="ErrorCode">
/// <see cref="PublicSqlErrorCodes.NotAMember"/> or <see cref="PublicSqlErrorCodes.MissingRole"/> when not.
/// </param>
/// <param name="Message">Human-readable reason.</param>
public sealed record PublicUserAuthResult(bool Allowed, string? ErrorCode = null, string? Message = null)
{
    public static readonly PublicUserAuthResult Ok = new(true);
}

/// <summary>
/// Checks that the acting user named by <c>X-User-Id</c> is a member of the API key's company and holds a
/// required role.
/// </summary>
/// <remarks>
/// The public API cannot use <c>User.HasCompanyRole(...)</c>: the API-key principal carries the key's
/// identity, not the acting user's, so it has no role claims to inspect. The roles have to be read from
/// the identity store instead.
/// <para>
/// <b>Gated off by default</b> (<see cref="PublicApiOptions.EnforceActingUserRoles"/>). The lookup keys on
/// <c>ApplicationUser.Id</c>, and whether <c>X-User-Id</c> occupies that id space or the Entra
/// <c>oid</c> space is not determinable from this repository: dataset creation writes
/// <c>DatasetUser.UserId</c> from the request header (which the client sets from the oid), while dataset
/// sharing writes it from <c>ApplicationUser.Id</c>, and no Entra user sync reconciles the two. If they
/// differ, enabling this denies every request. Confirm against the deployed database first, then turn it
/// on. Until then the per-user dataset, table, column and RLS grants are the authorization, which is why
/// shipping this off is defensible rather than merely convenient.
/// </para>
/// </remarks>
public interface IPublicApiUserAuthorizationService
{
    Task<PublicUserAuthResult> AuthorizeAsync(string companyId, string userId, string[] roleSuffixes,
        CancellationToken ct = default);
}

public class PublicApiUserAuthorizationService : IPublicApiUserAuthorizationService
{
    private readonly ApplicationDbContext _db;
    private readonly UserManagementDbContext _users;

    public PublicApiUserAuthorizationService(ApplicationDbContext db, UserManagementDbContext users)
    {
        _db = db;
        _users = users;
    }

    public async Task<PublicUserAuthResult> AuthorizeAsync(string companyId, string userId,
        string[] roleSuffixes, CancellationToken ct = default)
    {
        var isMember = await _db.CompanyMember
            .AnyAsync(m => m.CompanyId == companyId && m.ApplicationUserId == userId, ct);
        if (!isMember)
            return new PublicUserAuthResult(false, PublicSqlErrorCodes.NotAMember,
                "The acting user is not a member of this company.");

        // {companyId}_ADMIN passes every check, matching HasCompanyRole's short-circuit.
        var acceptable = roleSuffixes
            .Append(RoleSuffixes.Admin)
            .Select(suffix => RoleSuffixes.Role(companyId, suffix))
            .ToList();

        var roleIds = await _users.Roles
            .Where(r => r.Name != null && acceptable.Contains(r.Name))
            .Select(r => r.Id)
            .ToListAsync(ct);

        if (roleIds.Count == 0)
            return new PublicUserAuthResult(false, PublicSqlErrorCodes.MissingRole,
                "No role in this company grants query access. Ask an administrator to review the role setup.");

        var hasRole = await _users.UserRoles
            .AnyAsync(ur => ur.UserId == userId && roleIds.Contains(ur.RoleId), ct);

        return hasRole
            ? PublicUserAuthResult.Ok
            : new PublicUserAuthResult(false, PublicSqlErrorCodes.MissingRole,
                "The acting user does not hold a role that grants query access.");
    }
}
