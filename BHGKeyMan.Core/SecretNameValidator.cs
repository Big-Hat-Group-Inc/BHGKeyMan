using System.Text.RegularExpressions;

namespace BHGKeyMan.Core;

public sealed partial class SecretNameValidator : ISecretNameValidator
{
    [GeneratedRegex("^[A-Za-z][0-9A-Za-z-]{0,126}$")]
    private static partial Regex SecretNameRegex();

    public bool IsValid(string? secretName)
    {
        if (string.IsNullOrWhiteSpace(secretName))
        {
            return false;
        }

        return SecretNameRegex().IsMatch(secretName);
    }
}
