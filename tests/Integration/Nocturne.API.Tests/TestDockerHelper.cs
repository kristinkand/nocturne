using System.Diagnostics;

namespace Nocturne.API.Tests.Integration;

/// <summary>
/// Helper class to check Docker availability for integration tests
/// </summary>
public static class TestDockerHelper
{
    /// <summary>
    /// Checks if Docker is available and running
    /// </summary>
    /// <returns>True if Docker is available, false otherwise</returns>
    public static bool IsDockerAvailable()
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = "info",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                },
            };

            process.Start();
            process.WaitForExit(5000); // 5 second timeout

            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
