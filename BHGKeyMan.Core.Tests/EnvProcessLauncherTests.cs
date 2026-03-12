namespace BHGKeyMan.Core.Tests;

public class EnvProcessLauncherTests
{
    private static readonly string ExistingExecutablePath =
        Environment.GetEnvironmentVariable("COMSPEC") ?? "C:\\Windows\\System32\\cmd.exe";

    private static AppConfig CreateConfig(
        bool enableProcessLaunch = true,
        IReadOnlyList<string>? allowedExecutablePaths = null,
        IReadOnlyList<string>? allowedEnvironmentVariableNames = null)
    {
        return new AppConfig(
            new Uri("https://example.vault.azure.net/"),
            "00000000-0000-0000-0000-000000000000",
            "11111111-1111-1111-1111-111111111111",
            TimeSpan.FromHours(8),
            false,
            5,
            null,
            true,
            30,
            30,
            enableProcessLaunch,
            allowedExecutablePaths ?? [ExistingExecutablePath],
            allowedEnvironmentVariableNames ?? ["OPENAI_API_KEY"]);
    }

    [Fact]
    public void ReturnsFailureWhenProcessLaunchDisabled()
    {
        var launcher = new EnvProcessLauncher(CreateConfig(enableProcessLaunch: false));

        var result = launcher.Launch(ExistingExecutablePath, "", Array.Empty<SecretEnvMapping>(), _ => null);

        Assert.False(result.Success);
        Assert.Equal("Process launch is disabled by configuration.", result.Message);
    }

    [Fact]
    public void ReturnsFailureForMissingExecutable()
    {
        var launcher = new EnvProcessLauncher(CreateConfig());

        var result = launcher.Launch("C:/not/found/tool.exe", "", Array.Empty<SecretEnvMapping>(), _ => null);

        Assert.False(result.Success);
        Assert.Equal("Executable not found.", result.Message);
    }

    [Fact]
    public void ReturnsFailureForEmptyPath()
    {
        var launcher = new EnvProcessLauncher(CreateConfig());

        var result = launcher.Launch("", "", Array.Empty<SecretEnvMapping>(), _ => null);

        Assert.False(result.Success);
        Assert.Equal("Executable path is required.", result.Message);
    }

    [Fact]
    public void ReturnsFailureForExecutableOutsideAllowList()
    {
        var launcher = new EnvProcessLauncher(CreateConfig(allowedExecutablePaths: ["C:\\Tools\\approved.exe"]));

        var result = launcher.Launch(ExistingExecutablePath, "", Array.Empty<SecretEnvMapping>(), _ => null);

        Assert.False(result.Success);
        Assert.Contains("not approved", result.Message);
    }

    [Fact]
    public void ReturnsFailureForBlockedEnvironmentVariable()
    {
        var launcher = new EnvProcessLauncher(CreateConfig(allowedEnvironmentVariableNames: ["PATH", "OPENAI_API_KEY"]));
        var mappings = new[] { new SecretEnvMapping("my-secret", "PATH") };

        var result = launcher.Launch(ExistingExecutablePath, "", mappings, _ => "secret-value");

        Assert.False(result.Success);
        Assert.Contains("blocked by launch policy", result.Message);
    }

    [Fact]
    public void ReturnsFailureForEmptySecretValue()
    {
        var launcher = new EnvProcessLauncher(CreateConfig());
        var mappings = new[] { new SecretEnvMapping("my-secret", "OPENAI_API_KEY") };

        var result = launcher.Launch(ExistingExecutablePath, "", mappings, _ => null);

        Assert.False(result.Success);
        Assert.Contains("was empty or unavailable", result.Message);
    }
}
