using System.Diagnostics;

namespace Api.Tests.Infrastructure;

/// <summary>
/// Distingue «Docker assente» (salto PREVISTO) da «Docker presente ma il container non parte» (salto GUASTO):
/// i due casi si correggono in modi diversi e un motivo generico chiude l'indagine prima che inizi (#3978).
/// </summary>
internal static class DockerDaemonProbe
{
    private static readonly Lazy<bool> Available = new(Probe);

    public static bool IsAvailable() => Available.Value;

    private static bool Probe()
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = "version --format {{.Server.Version}}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };
            process.Start();
            return process.WaitForExit(15_000) && process.ExitCode == 0;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return false;
        }
    }
}
