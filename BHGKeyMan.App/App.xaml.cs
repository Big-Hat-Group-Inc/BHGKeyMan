using System.IO;
using System.Text.Json;
using System.Windows;
using Azure.Core;
using Azure.Identity;
using BHGKeyMan.Core;

namespace BHGKeyMan.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var config = LoadConfig();
        if (config is null)
        {
            Shutdown(1);
            return;
        }

        var validationErrors = config.Validate();
        if (validationErrors.Count > 0)
        {
            MessageBox.Show(
                $"Configuration validation failed:\n\n{string.Join("\n", validationErrors.Select(err => $"  - {err}"))}\n\nPlease fix appsettings.json and restart.",
                "BHGKeyMan \u2014 Configuration Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        var validator = new SecretNameValidator();
        var cache = new MemorySecretCache(config.SecretCacheTtl);

        TokenCredential? credential = null;
        ISecretStore secretStore;

        if (config.UseInMemoryDemoStore)
        {
            secretStore = new InMemorySecretStore(validator, cache, config.SecretCacheTtl);
        }
        else
        {
            credential = new InteractiveBrowserCredential(new InteractiveBrowserCredentialOptions
            {
                TenantId = config.TenantId,
                ClientId = config.ClientId,
                RedirectUri = new Uri("http://localhost")
            });
            secretStore = new CachedSecretStore(
                new AzureKeyVaultSecretStore(config, validator, credential), cache, config);
        }

        var processLauncher = new EnvProcessLauncher(config);

        var viewModel = new MainViewModel(secretStore, validator, processLauncher, config, credential);

        var mainWindow = new MainWindow
        {
            DataContext = viewModel
        };

        mainWindow.Show();
    }

    private static AppConfig? LoadConfig()
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(configPath))
        {
            MessageBox.Show(
                "Configuration file 'appsettings.json' was not found.\n\nCreate an appsettings.json in the application directory with your Key Vault settings.",
                "BHGKeyMan \u2014 Missing Configuration",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return null;
        }

        try
        {
            var json = File.ReadAllText(configPath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement.GetProperty("KeyVault");

            var uri = root.GetProperty("KeyVaultUri").GetString() ?? "https://example-kv.vault.azure.net/";
            var tenantId = root.GetProperty("TenantId").GetString() ?? "";
            var clientId = root.GetProperty("ClientId").GetString() ?? "";
            var ttlHours = root.GetProperty("SecretCacheTtlHours").GetInt32();
            var enableDpapi = root.GetProperty("EnableDpapiCache").GetBoolean();
            var maxRetryCount = root.GetProperty("MaxRetryCount").GetInt32();
            var useInMemoryDemoStore = root.TryGetProperty("UseInMemoryDemoStore", out var inMemoryElement) && inMemoryElement.GetBoolean();
            var dpapiCachePath = root.TryGetProperty("DpapiCachePath", out var cachePathElement)
                ? Environment.ExpandEnvironmentVariables(cachePathElement.GetString() ?? string.Empty)
                : null;
            var clipboardTimeout = root.TryGetProperty("ClipboardClearTimeoutSeconds", out var clipEl)
                ? clipEl.GetInt32()
                : 30;
            var revealTimeout = root.TryGetProperty("SecretRevealTimeoutSeconds", out var revealEl)
                ? revealEl.GetInt32()
                : 30;
            var enableProcessLaunch = root.TryGetProperty("EnableProcessLaunch", out var processLaunchEl) && processLaunchEl.GetBoolean();
            var allowedExecutablePaths = ReadStringArray(root, "AllowedExecutablePaths");
            var allowedEnvironmentVariableNames = ReadStringArray(root, "AllowedEnvironmentVariableNames");

            return new AppConfig(
                new Uri(uri),
                tenantId,
                clientId,
                TimeSpan.FromHours(ttlHours),
                enableDpapi,
                maxRetryCount,
                dpapiCachePath,
                useInMemoryDemoStore,
                clipboardTimeout,
                revealTimeout,
                enableProcessLaunch,
                allowedExecutablePaths,
                allowedEnvironmentVariableNames);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"{AppErrorMapper.ToUserMessage("Configuration loading", ex)}\n\nPlease fix appsettings.json and restart.",
                "BHGKeyMan \u2014 Configuration Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return null;
        }
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return property.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => Environment.ExpandEnvironmentVariables(item.GetString() ?? string.Empty))
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToArray();
    }
}
