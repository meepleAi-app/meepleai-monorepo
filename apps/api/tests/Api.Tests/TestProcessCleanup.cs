using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Xunit;

namespace Api.Tests;

/// <summary>
/// Test cleanup utility for killing hanging test processes.
/// Implements ICollectionFixture to run cleanup after all tests in assembly.
///
/// Usage: This cleanup runs automatically after all xUnit tests complete.
/// </summary>
public class TestProcessCleanup : IDisposable
{
    private static bool _cleanupExecuted = false;
    private static readonly object _lock = new object();

    public TestProcessCleanup()
    {
        // Setup runs before any test in the collection
    }

    public void Dispose()
    {
        // Only run cleanup once for the entire test assembly
        lock (_lock)
        {
            if (_cleanupExecuted)
            {
                return;
            }

            _cleanupExecuted = true;

            Console.WriteLine("\n🧹 Running cleanup script to kill hanging test processes...");

            try
            {
                // Only run on Windows (PowerShell script)
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var scriptPath = "../../../../tools/cleanup-test-processes.ps1";
                    var command = $"powershell.exe";
                    var arguments = $"-ExecutionPolicy Bypass -File {scriptPath}";

                    var processStartInfo = new ProcessStartInfo
                    {
                        FileName = command,
                        Arguments = arguments,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    using var process = Process.Start(processStartInfo);
                    if (process != null)
                    {
                        // Show output
                        var output = process.StandardOutput.ReadToEnd();
                        var error = process.StandardError.ReadToEnd();

                        // Wait with timeout to prevent indefinite hang (ISSUE-319 fix)
                        if (!process.WaitForExit(10000)) // 10 second timeout
                        {
                            Console.WriteLine("⚠️ Cleanup script timed out after 10s, killing process");
                            try
                            {
                                process.Kill();
                            }
                            catch (Exception killEx)
                            {
                                Console.Error.WriteLine($"Failed to kill cleanup process: {killEx.Message}");
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(output))
                        {
                            Console.WriteLine(output);
                        }

                        if (!string.IsNullOrWhiteSpace(error))
                        {
                            Console.Error.WriteLine(error);
                        }

                        Console.WriteLine("✅ Cleanup script completed");
                    }
                }
                else
                {
                    Console.WriteLine("ℹ️ Cleanup script skipped (Windows-only)");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"⚠️ Cleanup script failed (non-fatal): {ex.Message}");
                // Don't fail tests if cleanup fails
            }
        }
    }
}

/// <summary>
/// Collection definition to ensure cleanup runs after all tests.
/// xUnit runs this cleanup once after all tests in the collection complete.
/// </summary>
[CollectionDefinition("TestProcessCleanup")]
public class TestProcessCleanupCollection : ICollectionFixture<TestProcessCleanup>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}
