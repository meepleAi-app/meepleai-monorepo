using System.Globalization;
using System.Text;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Api.Services;

/// <summary>
/// Renders a self-contained HTML status page from a HealthReport.
/// No external CSS, JS, fonts, or images — works even when all other services are down.
/// </summary>
public static class StatusPageRenderer
{
    private const string GreenColor = "#22c55e";
    private const string YellowColor = "#eab308";
    private const string RedColor = "#ef4444";

    /// <summary>
    /// Generates a complete HTML page showing service health status.
    /// </summary>
    /// <param name="report">The health report from ASP.NET Core health checks.</param>
    /// <returns>A self-contained HTML string.</returns>
    public static string RenderHtml(HealthReport report)
    {
        var (overallColor, overallText) = GetOverallStatus(report);
        var grouped = GroupServicesByCategory(report.Entries);

        var sb = new StringBuilder(4096);

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"UTF-8\">");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine("  <meta http-equiv=\"refresh\" content=\"30\">");
        sb.AppendLine("  <title>MeepleAI Service Status</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine("    * { margin: 0; padding: 0; box-sizing: border-box; }");
        sb.AppendLine("    body { background: #1a1a2e; color: #e0e0e0; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif; padding: 24px; }");
        sb.AppendLine("    .container { max-width: 800px; margin: 0 auto; }");
        sb.AppendLine("    .banner { padding: 20px; border-radius: 8px; text-align: center; margin-bottom: 32px; }");
        sb.AppendLine("    .banner h1 { font-size: 24px; margin-bottom: 4px; color: #fff; }");
        sb.AppendLine("    .banner p { font-size: 14px; color: rgba(255,255,255,0.85); }");
        sb.AppendLine("    .section { margin-bottom: 24px; }");
        sb.AppendLine("    .section h2 { font-size: 16px; color: #a0a0c0; margin-bottom: 8px; text-transform: uppercase; letter-spacing: 1px; }");
        sb.AppendLine("    .service { display: flex; align-items: center; padding: 10px 14px; background: #16213e; border-radius: 6px; margin-bottom: 6px; font-family: 'Cascadia Code', 'Fira Code', 'Consolas', monospace; font-size: 14px; }");
        sb.AppendLine("    .dot { width: 10px; height: 10px; border-radius: 50%; margin-right: 12px; flex-shrink: 0; }");
        sb.AppendLine("    .name { flex: 1; font-weight: 600; }");
        sb.AppendLine("    .status-text { width: 90px; text-align: center; }");
        sb.AppendLine("    .duration { width: 80px; text-align: right; color: #888; }");
        sb.AppendLine("    .desc { width: 100%; padding: 2px 0 0 22px; font-size: 12px; color: #777; }");
        sb.AppendLine("    .service-wrap { margin-bottom: 6px; }");
        sb.AppendLine("    .footer { text-align: center; margin-top: 32px; font-size: 12px; color: #555; }");
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("  <div class=\"container\">");

        // Overall status banner
        sb.Append("    <div class=\"banner\" style=\"background: ").Append(overallColor).AppendLine("\">");
        sb.AppendLine("      <h1>MeepleAI Service Status</h1>");
        sb.Append("      <p>").Append(overallText).AppendLine("</p>");
        sb.AppendLine("    </div>");

        // Render each category
        foreach (var (categoryName, services) in grouped)
        {
            if (services.Count == 0)
                continue;

            sb.AppendLine("    <div class=\"section\">");
            sb.Append("      <h2>").Append(categoryName).AppendLine("</h2>");

            foreach (var (name, entry) in services)
            {
                var statusColor = entry.Status switch
                {
                    HealthStatus.Healthy => GreenColor,
                    HealthStatus.Degraded => YellowColor,
                    _ => RedColor
                };
                var durationMs = (int)entry.Duration.TotalMilliseconds;

                sb.AppendLine("      <div class=\"service-wrap\">");
                sb.AppendLine("        <div class=\"service\">");
                sb.Append("          <span class=\"dot\" style=\"background: ").Append(statusColor).AppendLine("\"></span>");
                sb.Append("          <span class=\"name\">").Append(System.Net.WebUtility.HtmlEncode(name)).AppendLine("</span>");
                sb.Append("          <span class=\"status-text\" style=\"color: ").Append(statusColor).Append("\">").Append(entry.Status).AppendLine("</span>");
                sb.Append("          <span class=\"duration\">").Append(durationMs.ToString(CultureInfo.InvariantCulture)).AppendLine(" ms</span>");
                sb.AppendLine("        </div>");

                if (!string.IsNullOrEmpty(entry.Description))
                {
                    sb.Append("        <div class=\"desc\">").Append(System.Net.WebUtility.HtmlEncode(entry.Description)).AppendLine("</div>");
                }

                sb.AppendLine("      </div>");
            }

            sb.AppendLine("    </div>");
        }

        // Footer
        sb.AppendLine("    <div class=\"footer\">");
        sb.Append("      <p>Last updated: ").Append(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)).AppendLine(" UTC</p>");
        sb.AppendLine("      <p>Auto-refreshes every 30 seconds</p>");
        sb.AppendLine("    </div>");

        sb.AppendLine("  </div>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    private static (string Color, string Text) GetOverallStatus(HealthReport report)
    {
        var hasUnhealthy = report.Entries.Values.Any(e => e.Status == HealthStatus.Unhealthy);
        if (hasUnhealthy)
            return (RedColor, "Service Disruption");

        var hasDegraded = report.Entries.Values.Any(e => e.Status == HealthStatus.Degraded);
        if (hasDegraded)
            return (YellowColor, "Partial Degradation");

        return (GreenColor, "All Systems Operational");
    }

    private static List<(string CategoryName, List<(string Name, HealthReportEntry Entry)> Services)> GroupServicesByCategory(
        IReadOnlyDictionary<string, HealthReportEntry> entries)
    {
        var core = new List<(string, HealthReportEntry)>();
        var ai = new List<(string, HealthReportEntry)>();
        var external = new List<(string, HealthReportEntry)>();
        var monitoring = new List<(string, HealthReportEntry)>();
        var other = new List<(string, HealthReportEntry)>();

        foreach (var (name, entry) in entries)
        {
            var tags = entry.Tags;
            if (tags.Any(t => string.Equals(t, "core", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(t, "db", StringComparison.OrdinalIgnoreCase)))
            {
                core.Add((name, entry));
            }
            else if (tags.Any(t => string.Equals(t, "ai", StringComparison.OrdinalIgnoreCase)))
            {
                ai.Add((name, entry));
            }
            else if (tags.Any(t => string.Equals(t, "external", StringComparison.OrdinalIgnoreCase)))
            {
                external.Add((name, entry));
            }
            else if (tags.Any(t => string.Equals(t, "monitoring", StringComparison.OrdinalIgnoreCase)))
            {
                monitoring.Add((name, entry));
            }
            else
            {
                other.Add((name, entry));
            }
        }

        var result = new List<(string, List<(string, HealthReportEntry)>)>
        {
            ("Core Services", core),
            ("AI Services", ai),
            ("External Services", external),
            ("Monitoring", monitoring),
            ("Other", other)
        };

        return result;
    }
}
