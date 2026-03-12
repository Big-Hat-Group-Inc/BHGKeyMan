using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BHGKeyMan.Core;

[SupportedOSPlatform("windows")]
public sealed class DpapiSecretCacheStore
{
    private readonly string _directoryPath;

    public DpapiSecretCacheStore(string directoryPath)
    {
        _directoryPath = directoryPath;
    }

    public void Save(SecretEntry entry)
    {
        var payload = JsonSerializer.Serialize(entry);
        var bytes = Encoding.UTF8.GetBytes(payload);
        var protectedBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);

        Directory.CreateDirectory(_directoryPath);
        File.WriteAllBytes(GetFilePath(entry.Name), protectedBytes);
    }

    public bool TryLoad(string secretName, out SecretEntry entry)
    {
        entry = null!;
        var filePath = GetFilePath(secretName);

        if (!File.Exists(filePath))
        {
            return false;
        }

        try
        {
            var protectedBytes = File.ReadAllBytes(filePath);
            var bytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
            var payload = Encoding.UTF8.GetString(bytes);
            var parsed = JsonSerializer.Deserialize<SecretEntry>(payload);

            if (parsed is null || parsed.ExpiresAtUtc <= DateTimeOffset.UtcNow)
            {
                TryDeleteFile(filePath);
                return false;
            }

            entry = parsed with { FromCache = true };
            return true;
        }
        catch (Exception ex) when (
            ex is CryptographicException or
            IOException or
            JsonException or
            FormatException)
        {
            TryDeleteFile(filePath);
            return false;
        }
    }

    public void Delete(string secretName)
    {
        TryDeleteFile(GetFilePath(secretName));
    }

    private string GetFilePath(string secretName) =>
        Path.Combine(_directoryPath, $"{secretName}.bin");

    private static void TryDeleteFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch (IOException)
        {
            // Best-effort deletion
        }
    }
}
