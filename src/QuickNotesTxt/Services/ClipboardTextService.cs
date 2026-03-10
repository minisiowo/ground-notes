using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia.Input.Platform;

namespace QuickNotesTxt.Services;

public static class ClipboardTextService
{
    public static async Task SetTextAsync(IClipboard clipboard, string text, CancellationToken cancellationToken = default)
    {
        if (await TrySetNativeLinuxClipboardAsync(text, cancellationToken))
        {
            return;
        }

        await clipboard.SetTextAsync(text);
    }

    private static async Task<bool> TrySetNativeLinuxClipboardAsync(string text, CancellationToken cancellationToken)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")))
        {
            return false;
        }

        const string executable = "/usr/bin/wl-copy";
        if (!File.Exists(executable))
        {
            return false;
        }

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = "--type text/plain;charset=utf-8",
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardInputEncoding = new UTF8Encoding(false)
            }
        };

        process.Start();
        await process.StandardInput.WriteAsync(text.AsMemory(), cancellationToken);
        await process.StandardInput.FlushAsync();
        process.StandardInput.Close();
        await process.WaitForExitAsync(cancellationToken);

        return process.ExitCode == 0;
    }
}
