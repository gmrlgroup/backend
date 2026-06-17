using System.Text;
using System.Text.RegularExpressions;
using Application.Shared.Enums;
using Application.Shared.Models;
using Application.Shared.Services;
using Renci.SshNet;

namespace Application.Services;

/// <summary>
/// Manages services over SSH for both platforms:
///   • Linux   — <c>systemctl</c>
///   • Windows — PowerShell service cmdlets (requires OpenSSH Server on the VM)
/// The credential's <see cref="ServerCredential.SecretEncrypted"/> is already decrypted by the
/// management service (plaintext password or PEM private key).
/// </summary>
public class SshServerExecutor : IRemoteServerExecutor
{
    public bool Supports(ServerPlatform platform) => true; // SSH covers Linux and Windows

    private static readonly Regex ValidServiceName = new(@"^[A-Za-z0-9._@:$ -]+$", RegexOptions.Compiled);

    public Task<List<RemoteServiceInfo>> DiscoverServicesAsync(ServerCredential credential, string host, CancellationToken ct = default)
        => Task.Run(() =>
        {
            using var client = CreateClient(credential, host);
            client.Connect();
            try
            {
                if (credential.Platform == ServerPlatform.Windows)
                {
                    var ps = "Get-Service | ForEach-Object { $_.Name + '|' + $_.DisplayName + '|' + $_.Status }";
                    var cmd = client.RunCommand(PowerShell(ps));
                    return ParseWindowsServices(cmd.Result);
                }

                var unit = client.RunCommand("systemctl list-units --type=service --all --plain --no-legend --no-pager");
                return ParseLinuxServices(unit.Result);
            }
            finally { client.Disconnect(); }
        }, ct);

    public Task<ServiceActionResult> StartServiceAsync(ServerCredential credential, string host, string serviceName, CancellationToken ct = default)
        => RunActionAsync(credential, host, start: true, serviceName, ct);

    public Task<ServiceActionResult> StopServiceAsync(ServerCredential credential, string host, string serviceName, CancellationToken ct = default)
        => RunActionAsync(credential, host, start: false, serviceName, ct);

    private Task<ServiceActionResult> RunActionAsync(ServerCredential credential, string host, bool start, string serviceName, CancellationToken ct)
        => Task.Run(() =>
        {
            if (!ValidServiceName.IsMatch(serviceName))
                return ServiceActionResult.Fail($"Invalid service name '{serviceName}'.");

            var verb = start ? "start" : "stop";
            using var client = CreateClient(credential, host);
            client.Connect();
            try
            {
                SshCommand cmd;
                if (credential.Platform == ServerPlatform.Windows)
                {
                    var cmdlet = start ? "Start-Service" : "Stop-Service";
                    var ps = $"try {{ {cmdlet} -Name '{serviceName}' -ErrorAction Stop }} catch {{ Write-Error $_.Exception.Message; exit 1 }}";
                    cmd = client.RunCommand(PowerShell(ps));
                }
                else
                {
                    // Non-interactive sudo so privileged service control works with passwordless sudo.
                    cmd = client.RunCommand($"sudo -n systemctl {verb} {serviceName}");
                }

                if (cmd.ExitStatus == 0)
                    return ServiceActionResult.Ok($"Service '{serviceName}' {verb} succeeded.");

                var error = string.IsNullOrWhiteSpace(cmd.Error) ? cmd.Result : cmd.Error;
                return ServiceActionResult.Fail($"{verb} failed (exit {cmd.ExitStatus}): {error.Trim()}");
            }
            finally { client.Disconnect(); }
        }, ct);

    /// <summary>Wraps a PowerShell snippet as a Base64 -EncodedCommand so it survives cmd/PowerShell SSH shells without quoting issues.</summary>
    private static string PowerShell(string script)
    {
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        return $"powershell -NoProfile -NonInteractive -EncodedCommand {encoded}";
    }

    private static SshClient CreateClient(ServerCredential credential, string host)
    {
        var port = credential.Port > 0 ? credential.Port : 22;
        var username = credential.Username ?? string.Empty;
        var secret = credential.SecretEncrypted ?? string.Empty;

        Renci.SshNet.ConnectionInfo connectionInfo;
        if (credential.AuthType == CredentialAuthType.SshKey)
        {
            using var keyStream = new MemoryStream(Encoding.UTF8.GetBytes(secret));
            var keyFile = new PrivateKeyFile(keyStream);
            connectionInfo = new Renci.SshNet.ConnectionInfo(host, port, username, new PrivateKeyAuthenticationMethod(username, keyFile));
        }
        else
        {
            connectionInfo = new Renci.SshNet.ConnectionInfo(host, port, username, new PasswordAuthenticationMethod(username, secret));
        }

        connectionInfo.Timeout = TimeSpan.FromSeconds(30);
        return new SshClient(connectionInfo);
    }

    private static List<RemoteServiceInfo> ParseLinuxServices(string output)
    {
        var services = new List<RemoteServiceInfo>();
        if (string.IsNullOrWhiteSpace(output)) return services;

        foreach (var rawLine in output.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0) continue;

            // Columns: UNIT LOAD ACTIVE SUB DESCRIPTION...
            var parts = line.Split((char[]?)null, 5, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4) continue;

            services.Add(new RemoteServiceInfo
            {
                Name = parts[0],
                DisplayName = parts.Length >= 5 ? parts[4] : parts[0],
                State = parts[3],
                IsRunning = string.Equals(parts[2], "active", StringComparison.OrdinalIgnoreCase)
            });
        }

        return services.OrderBy(s => s.Name).ToList();
    }

    private static List<RemoteServiceInfo> ParseWindowsServices(string output)
    {
        var services = new List<RemoteServiceInfo>();
        if (string.IsNullOrWhiteSpace(output)) return services;

        foreach (var rawLine in output.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0) continue;

            // Format: Name|DisplayName|Status
            var parts = line.Split('|');
            if (parts.Length < 3) continue;

            var name = parts[0];
            var status = parts[^1];
            var displayName = string.Join('|', parts[1..^1]);

            services.Add(new RemoteServiceInfo
            {
                Name = name,
                DisplayName = string.IsNullOrEmpty(displayName) ? name : displayName,
                State = status,
                IsRunning = string.Equals(status, "Running", StringComparison.OrdinalIgnoreCase)
            });
        }

        return services.OrderBy(s => s.Name).ToList();
    }
}
