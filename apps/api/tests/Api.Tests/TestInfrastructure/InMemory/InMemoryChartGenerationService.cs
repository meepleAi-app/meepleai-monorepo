namespace Api.Tests.TestInfrastructure.InMemory;

/// <summary>
/// In-memory chart generation service for unit tests
/// ISSUE-2601: Minimal valid PNG generation without ScottPlot dependency
/// </summary>
internal sealed class InMemoryChartGenerationService
{
    /// <summary>
    /// Generates a minimal valid PNG signature
    /// </summary>
    public byte[] GenerateLineChart(
        string title,
        string[] xLabels,
        double[] yValues,
        string yAxisLabel,
        int width = 800,
        int height = 400)
    {
        return GenerateMinimalPng();
    }

    /// <summary>
    /// Generates a minimal valid PNG signature
    /// </summary>
    public byte[] GenerateBarChart(
        string title,
        string[] categories,
        double[] values,
        string yAxisLabel,
        int width = 800,
        int height = 400)
    {
        return GenerateMinimalPng();
    }

    /// <summary>
    /// Generates a minimal valid PNG signature
    /// </summary>
    public byte[] GenerateMultiLineChart(
        string title,
        string[] xLabels,
        IReadOnlyDictionary<string, double[]> series,
        string yAxisLabel,
        int width = 800,
        int height = 400)
    {
        return GenerateMinimalPng();
    }

    /// <summary>
    /// Generates a minimal valid PNG signature
    /// </summary>
    public byte[] GenerateStackedBarChart(
        string title,
        string[] categories,
        IReadOnlyDictionary<string, double[]> series,
        string yAxisLabel,
        int width = 800,
        int height = 400)
    {
        return GenerateMinimalPng();
    }

    /// <summary>
    /// Returns minimal valid PNG signature
    /// PNG magic bytes: 0x89 0x50 0x4E 0x47 0x0D 0x0A 0x1A 0x0A
    /// </summary>
    private static byte[] GenerateMinimalPng()
    {
        return new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
    }
}
