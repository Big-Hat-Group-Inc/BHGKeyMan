using System.IO;
using Azure;
using Azure.Identity;

namespace BHGKeyMan.App;

internal static class AppErrorMapper
{
    public static string ToUserMessage(string operation, Exception exception)
    {
        return exception switch
        {
            AuthenticationFailedException => $"{operation} failed. Check your browser sign-in flow and tenant configuration.",
            UnauthorizedAccessException => $"{operation} failed because your account is not authorized for this action.",
            KeyNotFoundException => $"{operation} failed because the requested secret was not found.",
            RequestFailedException { Status: 403 } => $"{operation} failed because access was denied by Azure Key Vault.",
            RequestFailedException { Status: 404 } => $"{operation} failed because the requested secret does not exist.",
            RequestFailedException => $"{operation} failed because Azure Key Vault is unavailable or rejected the request.",
            InvalidOperationException => $"{operation} failed because the current application state is invalid.",
            IOException => $"{operation} failed because a local file or process resource was unavailable.",
            _ => $"{operation} failed. See diagnostics for more detail."
        };
    }
}
