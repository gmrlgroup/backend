namespace Application.Shared.Enums;

/// <summary>
/// Engine-neutral privilege level requested when provisioning a database user. Each engine maps this
/// to its own grant vocabulary (SQL Server db_* roles, Postgres/MySQL/ClickHouse GRANT statements).
/// </summary>
public enum DatabaseAccessLevel
{
    /// <summary>Can connect and SELECT.</summary>
    ReadOnly,

    /// <summary>ReadOnly plus INSERT/UPDATE/DELETE.</summary>
    ReadWrite,

    /// <summary>Full control of the database.</summary>
    Owner
}
