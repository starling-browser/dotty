using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Dotty.AI.Providers.Copilot;

/// <summary>
/// OS keychain abstraction for persisting OAuth tokens.
/// macOS: security CLI, Windows: credential file, Linux: config file fallback.
/// </summary>
public static class KeychainService
{
    private const string ServiceName = "com.dotty.copilot";
    private const string AccountName = "oauth-token";

    public static async Task<string?> GetTokenAsync()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return await MacOsGetAsync();

        return FileGet();
    }

    public static async Task SetTokenAsync(string token)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            await MacOsSetAsync(token);
        else
            FileSet(token);
    }

    public static async Task DeleteTokenAsync()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            await MacOsDeleteAsync();
        else
            FileDelete();
    }

    // ── macOS: security CLI ──

    private static async Task<string?> MacOsGetAsync()
    {
        try
        {
            var psi = new ProcessStartInfo("security",
                $"find-generic-password -s \"{ServiceName}\" -a \"{AccountName}\" -w")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            var process = Process.Start(psi)!;
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            return process.ExitCode == 0 ? output.Trim() : null;
        }
        catch
        {
            return null;
        }
    }

    private static async Task MacOsSetAsync(string token)
    {
        // Delete first to avoid duplicates
        await MacOsDeleteAsync();
        try
        {
            var psi = new ProcessStartInfo("security",
                $"add-generic-password -s \"{ServiceName}\" -a \"{AccountName}\" -w \"{token}\" -U")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            var process = Process.Start(psi)!;
            await process.WaitForExitAsync();
        }
        catch
        {
            // Fallback to file
            FileSet(token);
        }
    }

    private static async Task MacOsDeleteAsync()
    {
        try
        {
            var psi = new ProcessStartInfo("security",
                $"delete-generic-password -s \"{ServiceName}\" -a \"{AccountName}\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            var process = Process.Start(psi)!;
            await process.WaitForExitAsync();
        }
        catch
        {
            // Ignore — may not exist
        }
    }

    // ── File-based fallback (Windows/Linux) ──

    private static string TokenFilePath
    {
        get
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "Dotty", "copilot-token.json");
        }
    }

    private static string? FileGet()
    {
        try
        {
            return File.Exists(TokenFilePath) ? File.ReadAllText(TokenFilePath).Trim() : null;
        }
        catch
        {
            return null;
        }
    }

    private static void FileSet(string token)
    {
        try
        {
            var dir = Path.GetDirectoryName(TokenFilePath)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(TokenFilePath, token);
        }
        catch
        {
            // Non-critical
        }
    }

    private static void FileDelete()
    {
        try
        {
            if (File.Exists(TokenFilePath))
                File.Delete(TokenFilePath);
        }
        catch
        {
            // Ignore
        }
    }
}
