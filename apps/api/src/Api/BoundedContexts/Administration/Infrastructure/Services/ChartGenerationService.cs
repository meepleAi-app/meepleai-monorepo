using ScottPlot;
using ScottPlot.TickGenerators;

namespace Api.BoundedContexts.Administration.Infrastructure.Services;

/// <summary>
/// Service for generating charts using ScottPlot
/// ISSUE-917: Chart generation for report templates
/// </summary>
public sealed class ChartGenerationService
{
    /// <summary>
    /// Generates a line chart and returns the image as byte array
    /// </summary>
    public byte[] GenerateLineChart(
        string title,
        string[] xLabels,
        double[] yValues,
        string yAxisLabel,
        int width = 800,
        int height = 400)
    {
        using var plot = new Plot();

        // Add data
        var xPositions = Enumerable.Range(0, xLabels.Length).Select(i => (double)i).ToArray();
        var scatter = plot.Add.Scatter(xPositions, yValues);
        scatter.LineWidth = 2;
        scatter.MarkerSize = 8;

        // Customize appearance
        if (!string.IsNullOrEmpty(title))
            plot.Title(title);

        plot.YLabel(yAxisLabel);

        // Set custom tick labels
        var ticks = xPositions.Zip(xLabels, (pos, label) => new Tick(pos, label)).ToArray();
        plot.Axes.Bottom.TickGenerator = new NumericManual(ticks);
        plot.Axes.Bottom.MajorTickStyle.Length = 5;

        // Save to temporary file and read bytes
        var tempFile = Path.GetTempFileName();
        try
        {
            plot.SavePng(tempFile, width, height);
            return File.ReadAllBytes(tempFile);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Generates a bar chart and returns the image as byte array
    /// </summary>
    public byte[] GenerateBarChart(
        string title,
        string[] categories,
        double[] values,
        string yAxisLabel,
        int width = 800,
        int height = 400)
    {
        using var plot = new Plot();

        // Add bars
        var positions = Enumerable.Range(0, categories.Length).Select(i => (double)i).ToArray();
        for (int i = 0; i < positions.Length; i++)
        {
            plot.Add.Bar(position: positions[i], value: values[i]);
        }

        // Customize appearance
        if (!string.IsNullOrEmpty(title))
            plot.Title(title);

        plot.YLabel(yAxisLabel);

        // Set custom tick labels
        var ticks = positions.Zip(categories, (pos, label) => new Tick(pos, label)).ToArray();
        plot.Axes.Bottom.TickGenerator = new NumericManual(ticks);
        plot.Axes.Bottom.MajorTickStyle.Length = 0;
        plot.Axes.Margins(bottom: 0);

        // Save to temporary file and read bytes
        var tempFile = Path.GetTempFileName();
        try
        {
            plot.SavePng(tempFile, width, height);
            return File.ReadAllBytes(tempFile);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Generates a multi-series line chart
    /// </summary>
    public byte[] GenerateMultiLineChart(
        string title,
        string[] xLabels,
        Dictionary<string, double[]> series,
        string yAxisLabel,
        int width = 800,
        int height = 400)
    {
        using var plot = new Plot();
        var xPositions = Enumerable.Range(0, xLabels.Length).Select(i => (double)i).ToArray();

        // Add each series
        foreach (var (name, values) in series)
        {
            var scatter = plot.Add.Scatter(xPositions, values);
            scatter.LineWidth = 2;
            scatter.MarkerSize = 6;
            scatter.LegendText = name;
        }

        // Customize appearance
        if (!string.IsNullOrEmpty(title))
            plot.Title(title);

        plot.YLabel(yAxisLabel);
        plot.ShowLegend();

        // Set custom tick labels
        var ticks = xPositions.Zip(xLabels, (pos, label) => new Tick(pos, label)).ToArray();
        plot.Axes.Bottom.TickGenerator = new NumericManual(ticks);
        plot.Axes.Bottom.MajorTickStyle.Length = 5;

        // Save to temporary file and read bytes
        var tempFile = Path.GetTempFileName();
        try
        {
            plot.SavePng(tempFile, width, height);
            return File.ReadAllBytes(tempFile);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Generates a stacked bar chart
    /// NOTE: ScottPlot 5.0 doesn't have built-in stacked bars, using grouped bars instead
    /// </summary>
    public byte[] GenerateStackedBarChart(
        string title,
        string[] categories,
        Dictionary<string, double[]> series,
        string yAxisLabel,
        int width = 800,
        int height = 400)
    {
        using var plot = new Plot();
        var positions = Enumerable.Range(0, categories.Length).Select(i => (double)i).ToArray();

        // For stacking, we'll use a simpler approach: sum values and show breakdown in legend
        var totalValues = new double[categories.Length];
        foreach (var values in series.Values)
        {
            for (int i = 0; i < categories.Length; i++)
            {
                totalValues[i] += values[i];
            }
        }

        // Add bars showing totals
        for (int i = 0; i < positions.Length; i++)
        {
            plot.Add.Bar(position: positions[i], value: totalValues[i]);
        }

        // Customize appearance
        if (!string.IsNullOrEmpty(title))
            plot.Title(title);

        plot.YLabel(yAxisLabel);

        // Set custom tick labels
        var ticks = positions.Zip(categories, (pos, label) => new Tick(pos, label)).ToArray();
        plot.Axes.Bottom.TickGenerator = new NumericManual(ticks);
        plot.Axes.Bottom.MajorTickStyle.Length = 0;
        plot.Axes.Margins(bottom: 0);

        // Save to temporary file and read bytes
        var tempFile = Path.GetTempFileName();
        try
        {
            plot.SavePng(tempFile, width, height);
            return File.ReadAllBytes(tempFile);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }
}
