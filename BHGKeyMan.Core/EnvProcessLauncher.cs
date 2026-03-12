using System.ComponentModel;
using System.Diagnostics;

namespace BHGKeyMan.Core;

public sealed class EnvProcessLauncher : IEnvProcessLauncher
{
    private static readonly string[] BlockedEnvironmentVariableNames =
    [
        "PATH",
        "COMSPEC",
        "PATHEXT",
        "PSMODULEPATH",
        "DOTNET_ROOT"
    ];

    private readonly AppConfig _config;

    public EnvProcessLauncher(AppConfig config)
    {
        _config = config;
    }

    public OperationResult Launch(string executablePath, string arguments, IReadOnlyList<SecretEnvMapping> mappings, Func<string, string?> secretResolver)
    {
        if (!_config.EnableProcessLaunch)
        {
            return new OperationResult(false, "Process launch is disabled by configuration.");
        }

        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return new OperationResult(false, "Executable path is required.");
        }

        var normalizedExecutablePath = Path.GetFullPath(executablePath);
        if (!File.Exists(executablePath))
        {
            return new OperationResult(false, "Executable not found.");
        }

        if (!IsAllowedExecutablePath(normalizedExecutablePath))
        {
            return new OperationResult(false, $"Executable '{normalizedExecutablePath}' is not approved for secret injection.");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = normalizedExecutablePath,
            Arguments = arguments,
            UseShellExecute = false
        };

        foreach (var mapping in mappings)
        {
            var secretValue = secretResolver(mapping.SecretName);
            if (string.IsNullOrWhiteSpace(secretValue))
            {
                return new OperationResult(false, $"Secret '{mapping.SecretName}' was empty or unavailable.");
            }

            if (!IsAllowedEnvironmentVariableName(mapping.EnvironmentVariableName))
            {
                return new OperationResult(false, $"Environment variable '{mapping.EnvironmentVariableName}' is blocked by launch policy.");
            }

            startInfo.Environment[mapping.EnvironmentVariableName] = secretValue;
        }

        try
        {
            Process.Start(startInfo);
            return new OperationResult(true, "Process launched with process-scoped environment variables.");
        }
        catch (Exception ex) when (
            ex is InvalidOperationException or
            Win32Exception or
            IOException)
        {
            return new OperationResult(false, $"Failed to start process '{executablePath}': {ex.Message}");
        }
    }

    private bool IsAllowedExecutablePath(string executablePath)
    {
        var allowedPaths = _config.AllowedExecutablePaths ?? [];
        foreach (var allowedPath in allowedPaths)
        {
            var normalizedAllowedPath = Path.GetFullPath(allowedPath);
            if (string.Equals(executablePath, normalizedAllowedPath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (executablePath.StartsWith(AppendDirectorySeparator(normalizedAllowedPath), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsAllowedEnvironmentVariableName(string environmentVariableName)
    {
        if (string.IsNullOrWhiteSpace(environmentVariableName))
        {
            return false;
        }

        if (environmentVariableName.StartsWith("DOTNET_", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (BlockedEnvironmentVariableNames.Contains(environmentVariableName, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        return (_config.AllowedEnvironmentVariableNames ?? []).Contains(environmentVariableName, StringComparer.OrdinalIgnoreCase);
    }

    private static string AppendDirectorySeparator(string path)
    {
        if (path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar))
        {
            return path;
        }

        return path + Path.DirectorySeparatorChar;
    }
}
