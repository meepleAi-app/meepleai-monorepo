using System.Runtime.CompilerServices;
using QuestPDF.Infrastructure;

namespace Api.Infrastructure;

/// <summary>
/// Configures QuestPDF library settings at module initialization.
/// CRITICAL: Module initializer runs BEFORE any other code in the assembly.
/// </summary>
/// <remarks>
/// QuestPDF 2025.7.4+ requires license configuration before ANY API usage.
/// Using Module Initializer ensures license is set before type initialization.
/// Investigation: questpdf_api_investigation memory (2026-01-18)
/// </remarks>
internal static class QuestPdfConfiguration
{
    /// <summary>
    /// Sets QuestPDF Community license at module load time.
    /// </summary>
    [ModuleInitializer]
    internal static void Initialize()
    {
        // Community license valid for non-commercial/evaluation use
        QuestPDF.Settings.License = LicenseType.Community;
    }
}
