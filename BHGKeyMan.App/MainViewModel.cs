using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using Azure.Core;
using Azure.Identity;
using BHGKeyMan.Core;

namespace BHGKeyMan.App;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly ISecretStore _secretStore;
    private readonly ISecretNameValidator _secretNameValidator;
    private readonly IEnvProcessLauncher _processLauncher;
    private readonly AppConfig _config;
    private readonly TokenCredential? _credential;

    private string _identityStatus = "Not signed in";
    private string _statusMessage = "Ready";
    private string _retrieveSecretName = string.Empty;
    private string _setSecretName = string.Empty;
    private string _secretValueInput = string.Empty;
    private string _selectedSecretName = string.Empty;
    private string _retrievedSecretValue = string.Empty;
    private string _launchExecutablePath = string.Empty;
    private string _launchArguments = string.Empty;
    private string _launchEnvVarName = "OPENAI_API_KEY";
    private bool _isSecretRevealed;
    private CancellationTokenSource? _clipboardClearCts;
    private CancellationTokenSource? _revealCts;
    private string? _copiedSecretValue;

    public MainViewModel(
        ISecretStore secretStore,
        ISecretNameValidator secretNameValidator,
        IEnvProcessLauncher processLauncher,
        AppConfig config,
        TokenCredential? credential = null)
    {
        _secretStore = secretStore;
        _secretNameValidator = secretNameValidator;
        _processLauncher = processLauncher;
        _config = config;
        _credential = credential;

        Secrets = new ObservableCollection<string>();

        SignInCommand = new AsyncRelayCommand(SignInAsync, () => !_config.UseInMemoryDemoStore && !IsAnyOperationRunning, RefreshCommandStates);
        LoadSecretsCommand = new AsyncRelayCommand(LoadSecretsAsync, () => !IsAnyOperationRunning, RefreshCommandStates);
        GetSecretCommand = new AsyncRelayCommand(GetSecretAsync, CanGetSecret, RefreshCommandStates);
        SetSecretCommand = new AsyncRelayCommand(SetSecretAsync, CanSetSecret, RefreshCommandStates);
        LaunchProcessCommand = new AsyncRelayCommand(LaunchProcessAsync, CanLaunchProcess, RefreshCommandStates);
        CopySecretCommand = new RelayCommand(CopySecret, () => !IsAnyOperationRunning && !string.IsNullOrWhiteSpace(RetrievedSecretValue));
        ToggleRevealCommand = new RelayCommand(ToggleReveal, () => !string.IsNullOrWhiteSpace(RetrievedSecretValue));

        IdentityStatus = config.UseInMemoryDemoStore
            ? "Demo mode active"
            : $"Azure Key Vault mode for tenant {config.TenantId}";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<string> Secrets { get; }

    public AsyncRelayCommand SignInCommand { get; }
    public AsyncRelayCommand LoadSecretsCommand { get; }
    public AsyncRelayCommand GetSecretCommand { get; }
    public AsyncRelayCommand SetSecretCommand { get; }
    public AsyncRelayCommand LaunchProcessCommand { get; }
    public RelayCommand CopySecretCommand { get; }
    public RelayCommand ToggleRevealCommand { get; }

    public bool IsDemoMode => _config.UseInMemoryDemoStore;
    public bool IsProcessLaunchEnabled => _config.EnableProcessLaunch;
    public bool IsAnyOperationRunning =>
        SignInCommand.IsRunning ||
        LoadSecretsCommand.IsRunning ||
        GetSecretCommand.IsRunning ||
        SetSecretCommand.IsRunning ||
        LaunchProcessCommand.IsRunning;

    public string IdentityStatus
    {
        get => _identityStatus;
        set => SetProperty(ref _identityStatus, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string LaunchPolicyStatus =>
        _config.EnableProcessLaunch
            ? "Process launch is enabled for approved executables and approved environment variable names."
            : "Process launch is disabled by configuration.";

    public string RetrieveSecretName
    {
        get => _retrieveSecretName;
        set
        {
            if (SetProperty(ref _retrieveSecretName, value))
            {
                RefreshCommandStates();
            }
        }
    }

    public string SetSecretName
    {
        get => _setSecretName;
        set
        {
            if (SetProperty(ref _setSecretName, value))
            {
                RefreshCommandStates();
            }
        }
    }

    public string SecretValueInput
    {
        get => _secretValueInput;
        private set
        {
            if (SetProperty(ref _secretValueInput, value))
            {
                RefreshCommandStates();
            }
        }
    }

    public string SelectedSecretName
    {
        get => _selectedSecretName;
        set
        {
            if (SetProperty(ref _selectedSecretName, value))
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    RetrieveSecretName = value;
                    if (string.IsNullOrWhiteSpace(SetSecretName))
                    {
                        SetSecretName = value;
                    }
                }

                RefreshCommandStates();
            }
        }
    }

    public string RetrievedSecretValue
    {
        get => _retrievedSecretValue;
        private set
        {
            if (SetProperty(ref _retrievedSecretValue, value))
            {
                OnPropertyChanged(nameof(DisplayedSecretValue));
                IsSecretRevealed = false;
                RefreshCommandStates();
            }
        }
    }

    public string DisplayedSecretValue =>
        string.IsNullOrEmpty(RetrievedSecretValue)
            ? "No secret loaded"
            : IsSecretRevealed
                ? RetrievedSecretValue
                : new string('\u2022', Math.Min(RetrievedSecretValue.Length, 24));

    public bool IsSecretRevealed
    {
        get => _isSecretRevealed;
        set
        {
            if (SetProperty(ref _isSecretRevealed, value))
            {
                OnPropertyChanged(nameof(DisplayedSecretValue));
            }
        }
    }

    public string LaunchExecutablePath
    {
        get => _launchExecutablePath;
        set
        {
            if (SetProperty(ref _launchExecutablePath, value))
            {
                RefreshCommandStates();
            }
        }
    }

    public string LaunchArguments
    {
        get => _launchArguments;
        set => SetProperty(ref _launchArguments, value);
    }

    public string LaunchEnvVarName
    {
        get => _launchEnvVarName;
        set
        {
            if (SetProperty(ref _launchEnvVarName, value))
            {
                RefreshCommandStates();
            }
        }
    }

    private string EffectiveRetrieveSecretName =>
        string.IsNullOrWhiteSpace(SelectedSecretName) ? RetrieveSecretName : SelectedSecretName;

    public void UpdateSecretValueInput(string secretValue)
    {
        SecretValueInput = secretValue;
    }

    private bool CanGetSecret()
    {
        return !IsAnyOperationRunning && !string.IsNullOrWhiteSpace(EffectiveRetrieveSecretName);
    }

    private bool CanSetSecret()
    {
        return !IsAnyOperationRunning &&
               !string.IsNullOrWhiteSpace(SetSecretName) &&
               !string.IsNullOrWhiteSpace(SecretValueInput);
    }

    private bool CanLaunchProcess()
    {
        return !IsAnyOperationRunning &&
               _config.EnableProcessLaunch &&
               !string.IsNullOrWhiteSpace(LaunchExecutablePath) &&
               !string.IsNullOrWhiteSpace(LaunchEnvVarName) &&
               !string.IsNullOrWhiteSpace(EffectiveRetrieveSecretName);
    }

    private async Task SignInAsync()
    {
        if (_credential is null)
        {
            StatusMessage = "Demo mode does not require authentication.";
            return;
        }

        try
        {
            StatusMessage = "Signing in. Check your browser for the Azure sign-in prompt.";
            var context = new TokenRequestContext(["https://vault.azure.net/.default"]);
            await _credential.GetTokenAsync(context, CancellationToken.None);
            IdentityStatus = $"Signed in for tenant {_config.TenantId}";
            StatusMessage = "Sign-in successful.";
        }
        catch (Exception ex)
        {
            IdentityStatus = "Not signed in";
            StatusMessage = AppErrorMapper.ToUserMessage("Sign-in", ex);
        }
    }

    private async Task LoadSecretsAsync()
    {
        try
        {
            await RefreshSecretsAsync();
            StatusMessage = $"Loaded {Secrets.Count} secrets.";
        }
        catch (Exception ex)
        {
            StatusMessage = AppErrorMapper.ToUserMessage("Loading secrets", ex);
        }
    }

    private async Task GetSecretAsync()
    {
        try
        {
            var targetSecret = EffectiveRetrieveSecretName;
            if (!_secretNameValidator.IsValid(targetSecret))
            {
                StatusMessage = "Secret name is invalid.";
                return;
            }

            var result = await _secretStore.GetSecretAsync(targetSecret);
            RetrievedSecretValue = result.Value;
            StatusMessage = result.FromCache
                ? $"Retrieved '{targetSecret}' from cache."
                : $"Retrieved '{targetSecret}' from store.";
        }
        catch (Exception ex)
        {
            StatusMessage = AppErrorMapper.ToUserMessage("Retrieving the secret", ex);
        }
    }

    private async Task SetSecretAsync()
    {
        try
        {
            var result = await _secretStore.SetSecretAsync(SetSecretName, SecretValueInput);
            StatusMessage = result.Message;
            if (result.Success)
            {
                UpdateSecretValueInput(string.Empty);
                await RefreshSecretsAsync();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = AppErrorMapper.ToUserMessage("Saving the secret", ex);
        }
    }

    private async Task RefreshSecretsAsync()
    {
        var names = await _secretStore.ListSecretNamesAsync();
        Secrets.Clear();
        foreach (var name in names)
        {
            Secrets.Add(name);
        }
    }

    private void CopySecret()
    {
        if (string.IsNullOrWhiteSpace(RetrievedSecretValue))
        {
            StatusMessage = "No secret value is available to copy.";
            return;
        }

        var result = MessageBox.Show(
            "Copying a secret places it on the clipboard temporarily. Continue?",
            "BHGKeyMan \u2014 Copy Secret",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            StatusMessage = "Copy cancelled.";
            return;
        }

        Clipboard.SetText(RetrievedSecretValue);
        _copiedSecretValue = RetrievedSecretValue;
        StatusMessage = $"Secret copied to clipboard. It will be cleared in {_config.ClipboardClearTimeoutSeconds} seconds.";

        _clipboardClearCts?.Cancel();
        _clipboardClearCts = new CancellationTokenSource();
        _ = ClearClipboardAfterDelayAsync(_clipboardClearCts.Token);
    }

    private async Task ClearClipboardAfterDelayAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(_config.ClipboardClearTimeoutSeconds), token);
            if (Clipboard.ContainsText() && Clipboard.GetText() == _copiedSecretValue)
            {
                Clipboard.Clear();
                RetrievedSecretValue = string.Empty;
                StatusMessage = "Clipboard cleared and the in-memory secret view was reset.";
            }
        }
        catch (TaskCanceledException)
        {
        }
    }

    private async Task LaunchProcessAsync()
    {
        var secretName = EffectiveRetrieveSecretName;
        var envVarName = string.IsNullOrWhiteSpace(LaunchEnvVarName) ? "OPENAI_API_KEY" : LaunchEnvVarName;

        var confirmation = MessageBox.Show(
            $"Launch approved executable?\n\nExecutable: {LaunchExecutablePath}\nSecret: {secretName}\nEnvironment Variable: {envVarName}",
            "BHGKeyMan \u2014 Confirm Launch",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmation != MessageBoxResult.Yes)
        {
            StatusMessage = "Launch cancelled.";
            return;
        }

        try
        {
            var secretValue = await ResolveSecretValueAsync(secretName);
            if (secretValue is null)
            {
                return;
            }

            var result = _processLauncher.Launch(
                LaunchExecutablePath,
                LaunchArguments,
                [new SecretEnvMapping(secretName, envVarName)],
                _ => secretValue);

            StatusMessage = result.Message;
        }
        catch (Exception ex)
        {
            StatusMessage = AppErrorMapper.ToUserMessage("Launching the process", ex);
        }
    }

    private async Task<string?> ResolveSecretValueAsync(string secretName)
    {
        if (string.Equals(secretName, EffectiveRetrieveSecretName, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(RetrievedSecretValue))
        {
            return RetrievedSecretValue;
        }

        try
        {
            var fetched = await _secretStore.GetSecretAsync(secretName);
            return fetched.Value;
        }
        catch (Exception ex)
        {
            StatusMessage = AppErrorMapper.ToUserMessage("Resolving the secret for launch", ex);
            return null;
        }
    }

    private void ToggleReveal()
    {
        if (string.IsNullOrWhiteSpace(RetrievedSecretValue))
        {
            return;
        }

        IsSecretRevealed = !IsSecretRevealed;
        _revealCts?.Cancel();

        if (IsSecretRevealed)
        {
            _revealCts = new CancellationTokenSource();
            _ = AutoHideAfterDelayAsync(_revealCts.Token);
        }
    }

    private async Task AutoHideAfterDelayAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(_config.SecretRevealTimeoutSeconds), token);
            IsSecretRevealed = false;
            StatusMessage = "Secret value hidden again.";
        }
        catch (TaskCanceledException)
        {
        }
    }

    private void RefreshCommandStates()
    {
        OnPropertyChanged(nameof(IsAnyOperationRunning));
        SignInCommand.RaiseCanExecuteChanged();
        LoadSecretsCommand.RaiseCanExecuteChanged();
        GetSecretCommand.RaiseCanExecuteChanged();
        SetSecretCommand.RaiseCanExecuteChanged();
        LaunchProcessCommand.RaiseCanExecuteChanged();
        CopySecretCommand.RaiseCanExecuteChanged();
        ToggleRevealCommand.RaiseCanExecuteChanged();
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
