using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Api.Analyzers.Tests;

/// <summary>
/// Snapshot tests for <see cref="NoEnrichCatalogCoverCommandBypassAnalyzer"/> (MAI006).
///
/// MAI006 flags <c>new EnrichCatalogCoverCommand(...)</c> constructed outside
/// <c>WikidataCoverEnrichmentRunner</c>. After #3369 the runner is the single
/// source of truth for the enrich+record workflow (attempt-log + retry/dead-letter
/// + SSE); any other construction site dispatches the raw command and bypasses
/// that machinery. The analyzer matches by fully-qualified type name, so the inline
/// shims must use the same FQNs as the production types.
/// </summary>
public sealed class NoEnrichCatalogCoverCommandBypassAnalyzerTests
{
    /// <summary>
    /// Inline shims matching the production FQNs the analyzer compares against.
    /// The runner shim itself constructs the command — MAI006 must NOT fire there
    /// (the single legitimate construction site).
    /// </summary>
    private const string SharedShimSource = """
        namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.EnrichCatalogCover
        {
            public sealed class EnrichCatalogCoverCommand
            {
                public EnrichCatalogCoverCommand(System.Guid gameId, bool forceRefresh = false) { }
            }
        }
        namespace Api.BoundedContexts.SharedGameCatalog.Application.Services
        {
            using Api.BoundedContexts.SharedGameCatalog.Application.Commands.EnrichCatalogCover;
            public sealed class WikidataCoverEnrichmentRunner
            {
                public EnrichCatalogCoverCommand Build()
                {
                    // Legitimate construction — MAI006 must NOT fire here.
                    return new EnrichCatalogCoverCommand(System.Guid.NewGuid(), false);
                }
            }
        }
        """;

    [Fact]
    public async Task NewEnrichCatalogCoverCommand_InAdminHandler_ReportsMAI006()
    {
        var source = SharedShimSource + """

            namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands
            {
                using Api.BoundedContexts.SharedGameCatalog.Application.Commands.EnrichCatalogCover;
                public class SomeAdminHandler
                {
                    public EnrichCatalogCoverCommand Build()
                    {
                        return new EnrichCatalogCoverCommand(System.Guid.NewGuid());
                    }
                }
            }
            """;

        var diagnostics = await RunAnalyzerAsync(source);
        var mai006 = OnlyMai006(diagnostics);

        Assert.Single(mai006);
    }

    [Fact]
    public async Task NewEnrichCatalogCoverCommand_InsideRunner_DoesNotFlag()
    {
        // The runner is the single legitimate construction site. The shared shim
        // already constructs the command inside WikidataCoverEnrichmentRunner.Build —
        // running the analyzer over it must NOT produce MAI006.
        var source = SharedShimSource;

        var diagnostics = await RunAnalyzerAsync(source);
        AssertNoMai006(diagnostics);
    }

    [Fact]
    public async Task NewEnrichCatalogCoverCommand_InBatchHandler_ReportsMAI006()
    {
        // Regression guard for the exact #3369 anti-pattern: the batch handler
        // must NOT reconstruct the raw command — it goes through the runner.
        var source = SharedShimSource + """

            namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.EnrichCatalogCoverBatch
            {
                using Api.BoundedContexts.SharedGameCatalog.Application.Commands.EnrichCatalogCover;
                public class EnrichCatalogCoverBatchCommandHandler
                {
                    public EnrichCatalogCoverCommand Build(System.Guid id)
                    {
                        return new EnrichCatalogCoverCommand(id);
                    }
                }
            }
            """;

        var diagnostics = await RunAnalyzerAsync(source);
        Assert.Single(OnlyMai006(diagnostics));
    }

    [Fact]
    public async Task UnrelatedTypeCreation_DoesNotFlag()
    {
        var source = SharedShimSource + """

            namespace SubjectUnderTest
            {
                public sealed class OtherCommand { }
                public class Builder
                {
                    public OtherCommand Build() => new OtherCommand();
                }
            }
            """;

        var diagnostics = await RunAnalyzerAsync(source);
        AssertNoMai006(diagnostics);
    }

    // --------------------------------------------------------------------------
    // Helpers
    // --------------------------------------------------------------------------

    private static async Task<ImmutableArray<Diagnostic>> RunAnalyzerAsync(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest));
        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Runtime.CompilerServices.RuntimeHelpers).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location),
        };

        var compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: new[] { syntaxTree },
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var compileErrors = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error).ToImmutableArray();
        if (!compileErrors.IsEmpty)
        {
            throw new InvalidOperationException(
                "Inline test source did not compile cleanly. Errors:\n  " +
                string.Join("\n  ", compileErrors.Select(d => d.ToString())));
        }

        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new NoEnrichCatalogCoverCommandBypassAnalyzer());
        return await compilation.WithAnalyzers(analyzers).GetAnalyzerDiagnosticsAsync().ConfigureAwait(false);
    }

    private static ImmutableArray<Diagnostic> OnlyMai006(ImmutableArray<Diagnostic> diagnostics) =>
        diagnostics.Where(d => d.Id == NoEnrichCatalogCoverCommandBypassAnalyzer.RunnerBypassDiagnosticId).ToImmutableArray();

    private static void AssertNoMai006(ImmutableArray<Diagnostic> diagnostics) =>
        Assert.Empty(OnlyMai006(diagnostics));
}
