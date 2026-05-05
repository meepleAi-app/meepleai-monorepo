using System.Collections.Concurrent;
using System.Reflection;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor;

/// <summary>
/// Loads Mechanic Extractor prompts from embedded markdown resources shipped with the API
/// assembly. Lazily reads each file on first use and caches the text for the lifetime of
/// the process. ISSUE-524 / M1.2 — prompts are versioned via git (B4=A).
/// </summary>
internal sealed class EmbeddedMechanicPromptProvider : IMechanicPromptProvider
{
    private const string ResourcePrefix = "Api.BoundedContexts.SharedGameCatalog.Infrastructure.Prompts.MechanicExtractor.v1.";
    private static readonly Assembly Assembly = typeof(EmbeddedMechanicPromptProvider).Assembly;
    private readonly ConcurrentDictionary<string, string> _cache = new(StringComparer.Ordinal);

    public string PromptVersion => "v1.0.0";

    public string GetSystemPrompt() => LoadResource("system.md");

    public string GetSectionPrompt(MechanicSection section)
    {
        var fileName = section switch
        {
            MechanicSection.Summary => "summary.md",
            MechanicSection.Mechanics => "mechanics.md",
            MechanicSection.Victory => "victory.md",
            MechanicSection.Resources => "resources.md",
            MechanicSection.Phases => "phases.md",
            MechanicSection.Faq => "faq.md",
            _ => throw new ArgumentOutOfRangeException(nameof(section), section, "Unknown Mechanic section.")
        };

        return LoadResource(fileName);
    }

    private string LoadResource(string fileName)
    {
        return _cache.GetOrAdd(fileName, static name =>
        {
            var resourceName = ResourcePrefix + name;
            using var stream = Assembly.GetManifestResourceStream(resourceName)
                ?? throw new FileNotFoundException(
                    $"Embedded Mechanic Extractor prompt not found: {resourceName}. " +
                    "Ensure Api.csproj includes the BoundedContexts/SharedGameCatalog/Infrastructure/Prompts/MechanicExtractor/v1/*.md EmbeddedResource glob.");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        });
    }
}
