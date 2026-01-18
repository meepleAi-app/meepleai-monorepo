using System.Runtime.CompilerServices;
using QuestPDF.Infrastructure;

namespace Api.Tests.TestHelpers;

/// <summary>
/// Configures QuestPDF library settings for test assembly.
/// CRITICAL: Module initializer runs BEFORE any test code.
/// </summary>
/// <remarks>
/// QuestPDF 2025.7.4+ requires license configuration before ANY API usage.
/// This ensures tests can instantiate services that use QuestPDF formatters.
/// Investigation: questpdf_api_investigation memory (2026-01-18)
/// </remarks>
internal static class QuestPdfTestConfiguration
{
    /// <summary>
    /// Sets QuestPDF Community license at test module load time.
    /// </summary>
    [ModuleInitializer]
    internal static void Initialize()
    {
        // Community license valid for testing
        QuestPDF.Settings.License = LicenseType.Community;
    }
}
