namespace BHGKeyMan.Core.Tests;

public class AppConfigTests
{
    private static readonly string ValidTenantId = "aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee";
    private static readonly string ValidClientId = "ffffffff-1111-4aaa-8bbb-cccccccccccc";

    private static AppConfig CreateValid(bool demoMode = true, bool enableProcessLaunch = false)
    {
        return new AppConfig(
            new Uri("https://my-vault.vault.azure.net/"),
            demoMode ? "00000000-0000-0000-0000-000000000000" : ValidTenantId,
            demoMode ? "11111111-1111-1111-1111-111111111111" : ValidClientId,
            TimeSpan.FromHours(8),
            false,
            5,
            null,
            demoMode,
            30,
            30,
            enableProcessLaunch,
            enableProcessLaunch ? ["C:\\Tools\\approved.exe"] : [],
            enableProcessLaunch ? ["OPENAI_API_KEY"] : []);
    }

    [Fact]
    public void ValidDemoConfig_NoErrors()
    {
        var errors = CreateValid(demoMode: true).Validate();

        Assert.Empty(errors);
    }

    [Fact]
    public void ValidAzureConfig_NoErrors()
    {
        var errors = CreateValid(demoMode: false).Validate();

        Assert.Empty(errors);
    }

    [Fact]
    public void InvalidTenantGuid_InAzureMode_ReturnsError()
    {
        var config = CreateValid(demoMode: false) with { TenantId = "not-a-guid" };

        var errors = config.Validate();

        Assert.Contains(errors, e => e.Contains("TenantId"));
    }

    [Fact]
    public void InvalidClientGuid_InAzureMode_ReturnsError()
    {
        var config = CreateValid(demoMode: false) with { ClientId = "not-a-guid" };

        var errors = config.Validate();

        Assert.Contains(errors, e => e.Contains("ClientId"));
    }

    [Fact]
    public void ZeroTtl_ReturnsError()
    {
        var errors = (CreateValid() with { SecretCacheTtl = TimeSpan.Zero }).Validate();

        Assert.Contains(errors, e => e.Contains("SecretCacheTtlHours"));
    }

    [Fact]
    public void NegativeMaxRetry_ReturnsError()
    {
        var errors = (CreateValid() with { MaxRetryCount = -1 }).Validate();

        Assert.Contains(errors, e => e.Contains("MaxRetryCount"));
    }

    [Fact]
    public void PlaceholderValues_InDemoMode_NoErrors()
    {
        var config = new AppConfig(
            new Uri("https://example-kv.vault.azure.net/"),
            "00000000-0000-0000-0000-000000000000",
            "11111111-1111-1111-1111-111111111111",
            TimeSpan.FromHours(8),
            false,
            5,
            null,
            true);

        Assert.Empty(config.Validate());
    }

    [Fact]
    public void ZeroClipboardTimeout_ReturnsError()
    {
        var errors = (CreateValid() with { ClipboardClearTimeoutSeconds = 0 }).Validate();

        Assert.Contains(errors, e => e.Contains("ClipboardClearTimeoutSeconds"));
    }

    [Fact]
    public void ZeroRevealTimeout_ReturnsError()
    {
        var errors = (CreateValid() with { SecretRevealTimeoutSeconds = 0 }).Validate();

        Assert.Contains(errors, e => e.Contains("SecretRevealTimeoutSeconds"));
    }

    [Fact]
    public void ProcessLaunchEnabledWithoutAllowedExecutables_ReturnsError()
    {
        var config = CreateValid(enableProcessLaunch: true) with { AllowedExecutablePaths = [] };

        var errors = config.Validate();

        Assert.Contains(errors, e => e.Contains("AllowedExecutablePaths"));
    }

    [Fact]
    public void ProcessLaunchEnabledWithoutAllowedEnvironmentVariables_ReturnsError()
    {
        var config = CreateValid(enableProcessLaunch: true) with { AllowedEnvironmentVariableNames = [] };

        var errors = config.Validate();

        Assert.Contains(errors, e => e.Contains("AllowedEnvironmentVariableNames"));
    }
}
