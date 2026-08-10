using System;

namespace Application.Shared.Models.Data;

/// <summary>
/// Limits for the API-key public API's SQL execution endpoint. Bound from the <c>PublicApi</c>
/// configuration section.
/// </summary>
/// <remarks>
/// Every value is clamped on read rather than trusted, so a typo in configuration cannot widen a limit
/// past what the engines themselves enforce. These caps sit <i>inside</i> the execution services'
/// ceilings (5000 rows, 60s) — they never raise them.
/// </remarks>
public class PublicApiOptions
{
    /// <summary>Wall-clock budget for one query. Bounds the external path, which has none of its own.</summary>
    public int SqlTimeoutSeconds { get; set; } = 20;

    /// <summary>Row cap applied when the caller does not ask for one.</summary>
    public int DefaultMaxRows { get; set; } = 100;

    /// <summary>
    /// Hard ceiling on rows, whatever the caller asks for. Lower than the engines' 5000 because this
    /// payload crosses a network hop and part of it lands in a language model's context.
    /// </summary>
    public int MaxMaxRows { get; set; } = 1000;

    /// <summary>Longest accepted SQL string.</summary>
    public int MaxSqlLength { get; set; } = 20_000;

    /// <summary>
    /// When true, additionally require the acting user to be a company member holding QUERY or DATA_ADMIN.
    /// Off by default: it depends on <c>X-User-Id</c> occupying the same id space as
    /// <c>ApplicationUser.Id</c>, which is not verifiable from source. Turn it on only after confirming
    /// that in the deployed environment, or it silently denies everyone.
    /// </summary>
    public bool EnforceActingUserRoles { get; set; } = false;

    /// <summary>Sliding-window request budget per (API key, acting user).</summary>
    public int RequestsPerMinute { get; set; } = 60;

    /// <summary>
    /// Concurrent executions permitted per API key. Matters more than the per-minute rate: each DuckDB
    /// read opens a file handle, and a handful of concurrent full scans exhausts I/O long before a
    /// per-minute counter trips.
    /// </summary>
    public int MaxConcurrent { get; set; } = 4;

    /// <summary>Queue depth once <see cref="MaxConcurrent"/> is reached.</summary>
    public int ConcurrencyQueueLimit { get; set; } = 2;

    public int EffectiveTimeoutSeconds => Math.Clamp(SqlTimeoutSeconds, 1, 60);
    public int EffectiveMaxSqlLength => Math.Clamp(MaxSqlLength, 100, 200_000);
    public int EffectiveMaxMaxRows => Math.Clamp(MaxMaxRows, 1, 5000);
    public int EffectiveDefaultMaxRows => Math.Clamp(DefaultMaxRows, 1, EffectiveMaxMaxRows);
    public int EffectiveRequestsPerMinute => Math.Clamp(RequestsPerMinute, 1, 10_000);
    public int EffectiveMaxConcurrent => Math.Clamp(MaxConcurrent, 1, 64);
    public int EffectiveConcurrencyQueueLimit => Math.Clamp(ConcurrencyQueueLimit, 0, 256);

    /// <summary>Clamps a caller-supplied row request into the permitted range.</summary>
    public int ResolveRowCap(int? requested) =>
        requested is null or <= 0
            ? EffectiveDefaultMaxRows
            : Math.Clamp(requested.Value, 1, EffectiveMaxMaxRows);
}
