// =============================================================================
// MeepleAI - RAG Plugin System
// Issue #3424 - Transform/Filter Plugins
// =============================================================================

using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Base;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Contracts;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Models;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Implementations.Transform;

/// <summary>
/// Output formatting plugin for transforming response format.
/// Supports markdown, JSON, HTML, and plain text output formats.
/// </summary>
[RagPlugin("transform-formatter-v1",
    Category = PluginCategory.Transform,
    Name = "Formatter",
    Description = "Transforms output to different formats (markdown, json, html, plain)",
    Author = "MeepleAI")]
public sealed class TransformFormatterPlugin : RagPluginBase
{
    /// <inheritdoc />
    public override string Id => "transform-formatter-v1";

    /// <inheritdoc />
    public override string Name => "Formatter";

    /// <inheritdoc />
    public override string Version => "1.0.0";

    /// <inheritdoc />
    public override PluginCategory Category => PluginCategory.Transform;

    /// <inheritdoc />
    protected override string Description => "Transforms output to different formats";

    /// <inheritdoc />
    protected override IReadOnlyList<string> Tags => ["transform", "formatter", "output", "conversion"];

    /// <inheritdoc />
    protected override IReadOnlyList<string> Capabilities => ["markdown", "json", "html", "plain"];

    private static readonly JsonSerializerOptions s_jsonOptions = new() { WriteIndented = false };
    private static readonly JsonSerializerOptions s_jsonOptionsPretty = new() { WriteIndented = true };

    // Compiled regex patterns for markdown processing
    private static readonly Regex s_boldAsterisks = new(@"\*\*(.+?)\*\*", RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1));
    private static readonly Regex s_boldUnderscores = new(@"__(.+?)__", RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1));
    private static readonly Regex s_italicAsterisk = new(@"\*(.+?)\*", RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1));
    private static readonly Regex s_italicUnderscore = new(@"_(.+?)_", RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1));
    private static readonly Regex s_markdownLink = new(@"\[(.+?)\]\(.+?\)", RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1));
    private static readonly Regex s_codeBlockFenced = new(@"```[\s\S]*?```", RegexOptions.Multiline | RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1));
    private static readonly Regex s_codeInline = new(@"`(.+?)`", RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1));
    private static readonly Regex s_headers = new(@"^#{1,6}\s+", RegexOptions.Multiline | RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1));
    private static readonly Regex s_unorderedList = new(@"^[\-\*\+]\s+", RegexOptions.Multiline | RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1));
    private static readonly Regex s_orderedList = new(@"^\d+\.\s+", RegexOptions.Multiline | RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1));

    public TransformFormatterPlugin(ILogger<TransformFormatterPlugin> logger) : base(logger)
    {
    }

    /// <inheritdoc />
    protected override Task<PluginOutput> ExecuteCoreAsync(
        PluginInput input,
        PluginConfig config,
        CancellationToken cancellationToken)
    {
        var (response, sources) = ParsePayload(input.Payload);

        if (string.IsNullOrWhiteSpace(response))
        {
            return Task.FromResult(PluginOutput.Failed(input.ExecutionId, "Response is required", "MISSING_RESPONSE"));
        }

        var customConfig = ParseCustomConfig(config);

        var formatted = customConfig.OutputFormat switch
        {
            "json" => FormatAsJson(response, sources, customConfig),
            "html" => FormatAsHtml(response, sources, customConfig),
            "plain" => FormatAsPlainText(response, sources, customConfig),
            _ => FormatAsMarkdown(response, sources, customConfig)
        };

        var result = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            formatted = formatted,
            format = customConfig.OutputFormat,
            originalLength = response.Length,
            formattedLength = formatted.Length
        }));

        Logger.LogInformation(
            "Formatted response: Format={Format}, OriginalLength={Original}, FormattedLength={Formatted}",
            customConfig.OutputFormat, response.Length, formatted.Length);

        return Task.FromResult(PluginOutput.Successful(input.ExecutionId, result));
    }

    private static string FormatAsMarkdown(string response, List<SourceItem> sources, FormatterConfig config)
    {
        var sb = new StringBuilder();

        // Add title if provided
        if (!string.IsNullOrEmpty(config.Title))
        {
            sb.Append(CultureInfo.InvariantCulture, $"# {config.Title}");
            sb.AppendLine();
            sb.AppendLine();
        }

        // Main content
        sb.AppendLine(response);

        // Add sources if requested and available
        if (config.IncludeSources && sources.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("## Sources");
            sb.AppendLine();

            foreach (var source in sources)
            {
                if (!string.IsNullOrEmpty(source.Url))
                {
                    sb.Append(CultureInfo.InvariantCulture, $"- [{source.Title ?? source.Url}]({source.Url})");
                    sb.AppendLine();
                }
                else
                {
                    sb.Append(CultureInfo.InvariantCulture, $"- {source.Title ?? source.Id}");
                    sb.AppendLine();
                }
            }
        }

        // Add timestamp if requested
        if (config.IncludeTimestamp)
        {
            sb.AppendLine();
            sb.Append(CultureInfo.InvariantCulture, $"*Generated at: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC*");
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    private static string FormatAsJson(string response, List<SourceItem> sources, FormatterConfig config)
    {
        var output = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["response"] = response
        };

        if (!string.IsNullOrEmpty(config.Title))
        {
            output["title"] = config.Title;
        }

        if (config.IncludeSources && sources.Count > 0)
        {
            output["sources"] = sources.Select(s => new
            {
                id = s.Id,
                title = s.Title,
                url = s.Url
            }).ToList();
        }

        if (config.IncludeTimestamp)
        {
            output["generatedAt"] = DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        }

        return JsonSerializer.Serialize(output, config.PrettyPrint ? s_jsonOptionsPretty : s_jsonOptions);
    }

    private static string FormatAsHtml(string response, List<SourceItem> sources, FormatterConfig config)
    {
        var sb = new StringBuilder();

        sb.AppendLine("<div class=\"rag-response\">");

        // Add title if provided
        if (!string.IsNullOrEmpty(config.Title))
        {
            sb.Append(CultureInfo.InvariantCulture, $"  <h1>{HttpUtility.HtmlEncode(config.Title)}</h1>");
            sb.AppendLine();
        }

        // Main content - convert basic markdown to HTML
        var htmlContent = ConvertBasicMarkdownToHtml(response);
        sb.Append(CultureInfo.InvariantCulture, $"  <div class=\"content\">{htmlContent}</div>");
        sb.AppendLine();

        // Add sources if requested and available
        if (config.IncludeSources && sources.Count > 0)
        {
            sb.AppendLine("  <hr />");
            sb.AppendLine("  <div class=\"sources\">");
            sb.AppendLine("    <h2>Sources</h2>");
            sb.AppendLine("    <ul>");

            foreach (var source in sources)
            {
                if (!string.IsNullOrEmpty(source.Url))
                {
                    sb.Append(CultureInfo.InvariantCulture, $"      <li><a href=\"{HttpUtility.HtmlAttributeEncode(source.Url)}\">{HttpUtility.HtmlEncode(source.Title ?? source.Url)}</a></li>");
                    sb.AppendLine();
                }
                else
                {
                    sb.Append(CultureInfo.InvariantCulture, $"      <li>{HttpUtility.HtmlEncode(source.Title ?? source.Id)}</li>");
                    sb.AppendLine();
                }
            }

            sb.AppendLine("    </ul>");
            sb.AppendLine("  </div>");
        }

        // Add timestamp if requested
        if (config.IncludeTimestamp)
        {
            sb.Append(CultureInfo.InvariantCulture, $"  <p class=\"timestamp\"><em>Generated at: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</em></p>");
            sb.AppendLine();
        }

        sb.AppendLine("</div>");

        return sb.ToString();
    }

    private static string FormatAsPlainText(string response, List<SourceItem> sources, FormatterConfig config)
    {
        var sb = new StringBuilder();

        // Add title if provided
        if (!string.IsNullOrEmpty(config.Title))
        {
            sb.AppendLine(config.Title);
            sb.AppendLine(new string('=', config.Title.Length));
            sb.AppendLine();
        }

        // Main content - strip markdown formatting
        var plainText = StripMarkdownFormatting(response);
        sb.AppendLine(plainText);

        // Add sources if requested and available
        if (config.IncludeSources && sources.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine("Sources:");

            foreach (var source in sources)
            {
                var display = source.Title ?? source.Url ?? source.Id;
                if (!string.IsNullOrEmpty(source.Url) && !string.Equals(source.Url, display, StringComparison.Ordinal))
                {
                    sb.Append(CultureInfo.InvariantCulture, $"  - {display} ({source.Url})");
                    sb.AppendLine();
                }
                else
                {
                    sb.Append(CultureInfo.InvariantCulture, $"  - {display}");
                    sb.AppendLine();
                }
            }
        }

        // Add timestamp if requested
        if (config.IncludeTimestamp)
        {
            sb.AppendLine();
            sb.Append(CultureInfo.InvariantCulture, $"Generated at: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    private static string ConvertBasicMarkdownToHtml(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
        {
            return string.Empty;
        }

        // Very basic markdown to HTML conversion
        var html = HttpUtility.HtmlEncode(markdown);

        // Bold: **text** or __text__
        html = s_boldAsterisks.Replace(html, "<strong>$1</strong>");
        html = s_boldUnderscores.Replace(html, "<strong>$1</strong>");

        // Italic: *text* or _text_
        html = s_italicAsterisk.Replace(html, "<em>$1</em>");
        html = s_italicUnderscore.Replace(html, "<em>$1</em>");

        // Line breaks
        html = html.Replace("\n\n", "</p><p>", StringComparison.Ordinal);
        html = html.Replace("\n", "<br />", StringComparison.Ordinal);

        return $"<p>{html}</p>";
    }

    private static string StripMarkdownFormatting(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
        {
            return string.Empty;
        }

        var text = markdown;

        // Remove bold/italic markers
        text = s_boldAsterisks.Replace(text, "$1");
        text = s_boldUnderscores.Replace(text, "$1");
        text = s_italicAsterisk.Replace(text, "$1");
        text = s_italicUnderscore.Replace(text, "$1");

        // Remove links but keep text: [text](url) -> text
        text = s_markdownLink.Replace(text, "$1");

        // Remove code blocks
        text = s_codeBlockFenced.Replace(text, "");
        text = s_codeInline.Replace(text, "$1");

        // Remove headers markers
        text = s_headers.Replace(text, "");

        // Remove list markers
        text = s_unorderedList.Replace(text, "  - ");
        text = s_orderedList.Replace(text, "  ");

        return text.Trim();
    }

    private static (string Response, List<SourceItem> Sources) ParsePayload(JsonDocument payload)
    {
        var response = string.Empty;
        var sources = new List<SourceItem>();

        if (payload.RootElement.TryGetProperty("response", out var respElement))
        {
            response = respElement.GetString() ?? string.Empty;
        }

        if (payload.RootElement.TryGetProperty("sources", out var sourcesElement) &&
            sourcesElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var src in sourcesElement.EnumerateArray())
            {
                var id = src.TryGetProperty("id", out var i) ? i.GetString() ?? "" : "";
                var title = src.TryGetProperty("title", out var t) ? t.GetString() : null;
                var url = src.TryGetProperty("url", out var u) ? u.GetString() : null;
                sources.Add(new SourceItem(id, title, url));
            }
        }

        return (response, sources);
    }

    private static FormatterConfig ParseCustomConfig(PluginConfig config)
    {
        if (config.CustomConfig == null)
        {
            return new FormatterConfig();
        }

        var root = config.CustomConfig.RootElement;
        return new FormatterConfig
        {
            OutputFormat = root.TryGetProperty("outputFormat", out var of) ? of.GetString() ?? "markdown" : "markdown",
            Title = root.TryGetProperty("title", out var t) ? t.GetString() : null,
            IncludeSources = !root.TryGetProperty("includeSources", out var iSrc) || iSrc.GetBoolean(),
            IncludeTimestamp = root.TryGetProperty("includeTimestamp", out var iTs) && iTs.GetBoolean(),
            PrettyPrint = !root.TryGetProperty("prettyPrint", out var pp) || pp.GetBoolean()
        };
    }

    /// <inheritdoc />
    protected override ValidationResult ValidateInputCore(PluginInput input)
    {
        var errors = new List<ValidationError>();

        if (!input.Payload.RootElement.TryGetProperty("response", out var respElement) ||
            string.IsNullOrWhiteSpace(respElement.GetString()))
        {
            errors.Add(new ValidationError
            {
                Message = "Response is required for formatting",
                PropertyPath = "payload.response",
                Code = "MISSING_RESPONSE"
            });
        }

        return errors.Count == 0 ? ValidationResult.Success() : ValidationResult.Failure([.. errors]);
    }

    /// <inheritdoc />
    protected override JsonDocument CreateInputSchema()
    {
        return CreateSchemaFromJson("""
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "object",
            "properties": {
                "response": { "type": "string" },
                "sources": {
                    "type": "array",
                    "items": {
                        "type": "object",
                        "properties": {
                            "id": { "type": "string" },
                            "title": { "type": "string" },
                            "url": { "type": "string" }
                        }
                    }
                }
            },
            "required": ["response"]
        }
        """);
    }

    /// <inheritdoc />
    protected override JsonDocument CreateOutputSchema()
    {
        return CreateSchemaFromJson("""
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "object",
            "properties": {
                "formatted": { "type": "string" },
                "format": { "type": "string" },
                "originalLength": { "type": "integer" },
                "formattedLength": { "type": "integer" }
            },
            "required": ["formatted", "format"]
        }
        """);
    }

    /// <inheritdoc />
    protected override JsonDocument CreateConfigSchema()
    {
        return CreateSchemaFromJson("""
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "object",
            "properties": {
                "outputFormat": {
                    "type": "string",
                    "enum": ["markdown", "json", "html", "plain"],
                    "default": "markdown"
                },
                "title": { "type": "string" },
                "includeSources": { "type": "boolean", "default": true },
                "includeTimestamp": { "type": "boolean", "default": false },
                "prettyPrint": { "type": "boolean", "default": true }
            }
        }
        """);
    }

    private sealed record SourceItem(string Id, string? Title, string? Url);

    private sealed class FormatterConfig
    {
        public string OutputFormat { get; init; } = "markdown";
        public string? Title { get; init; }
        public bool IncludeSources { get; init; } = true;
        public bool IncludeTimestamp { get; init; }
        public bool PrettyPrint { get; init; } = true;
    }
}
