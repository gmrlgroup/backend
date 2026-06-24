using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Shared.Models.Data;

public class DuckdbOption
{
    public string DuckdbFilePath { get; set; } = default!;

    /// <summary>
    /// Directory DuckDB installs/caches loadable extensions (e.g. the <c>excel</c> extension) into.
    /// Set this explicitly so extension installs do NOT fall back to the running account's home
    /// directory — under a Windows service / IIS app pool identity that resolves to
    /// <c>C:\Windows\System32\config\systemprofile</c>, which is locked down and makes
    /// <c>INSTALL excel</c> fail. When unset, defaults to a <c>.duckdb_ext</c> folder next to the
    /// DuckDB data files (see <see cref="ResolveExtensionDirectory"/>).
    /// </summary>
    public string? ExtensionDirectory { get; set; }

    /// <summary>The effective extension directory: the configured value, or a folder beside the data files.</summary>
    public string ResolveExtensionDirectory() =>
        string.IsNullOrWhiteSpace(ExtensionDirectory)
            ? System.IO.Path.Combine(DuckdbFilePath, ".duckdb_ext")
            : ExtensionDirectory;
}

