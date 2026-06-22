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
/// Snapshot tests for <see cref="NoVectorDocumentBypassAnalyzer"/> (MAI004 + MAI005).
///
/// MAI004 flags <c>IPdfIndexingPipeline? pipeline = null</c> ctor params.
/// MAI005 flags <c>new VectorDocumentEntity {{ ... }}</c> outside KnowledgeBaseMappers.
///
/// The analyzer matches by fully-qualified type name, so inline shims must use the
/// same FQN as the production types.
/// </summary>
public sealed class NoVectorDocumentBypassAnalyzerTests
{
    /// <summary>
    /// Inline shims matching the production FQNs the analyzer compares against.
    /// </summary>
    private const string SharedShimSource = """
        namespace Api.BoundedContexts.KnowledgeBase.Application.Services
        {
            public interface IPdfIndexingPipeline { }
        }
        namespace Api.Infrastructure.Entities
        {
            public sealed class VectorDocumentEntity
            {
                public System.Guid Id { get; set; }
                public string? Metadata { get; set; }
            }
        }
        namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence.Mappers
        {
            using Api.Infrastructure.Entities;
            public static class KnowledgeBaseMappers
            {
                public static VectorDocumentEntity ToEntity()
                {
                    // Legitimate mapper construction — MAI005 must NOT fire here.
                    return new VectorDocumentEntity { Id = System.Guid.NewGuid() };
                }
            }
        }
        """;

    // --------------------------------------------------------------------------
    // MAI004 — IPdfIndexingPipeline? = null forbidden ctor parameter
    // --------------------------------------------------------------------------

    [Fact]
    public async Task NullablePipelineWithNullDefault_ReportsMAI004()
    {
        var source = SharedShimSource + """

            #nullable enable
            namespace SubjectUnderTest
            {
                using Api.BoundedContexts.KnowledgeBase.Application.Services;
                public class Handler
                {
                    public Handler(IPdfIndexingPipeline? pipeline = null) { }
                }
            }
            """;

        var diagnostics = await RunAnalyzerAsync(source);
        var mai004 = OnlyMai004(diagnostics);

        Assert.Single(mai004);
        Assert.Contains("'pipeline'", mai004[0].GetMessage());
    }

    [Fact]
    public async Task RequiredPipelineCtorParam_DoesNotFlag()
    {
        var source = SharedShimSource + """

            #nullable enable
            namespace SubjectUnderTest
            {
                using Api.BoundedContexts.KnowledgeBase.Application.Services;
                public class Handler
                {
                    public Handler(IPdfIndexingPipeline pipeline) { }
                }
            }
            """;

        var diagnostics = await RunAnalyzerAsync(source);
        AssertNoMaiDiagnostics(diagnostics);
    }

    [Fact]
    public async Task NullablePipelineWithoutDefault_DoesNotFlag()
    {
        // The BLOCKER pattern is specifically nullable + null default. A nullable
        // param without a default is unusual but doesn't paves the way for the
        // else-branch fallback — left for design judgement.
        var source = SharedShimSource + """

            #nullable enable
            namespace SubjectUnderTest
            {
                using Api.BoundedContexts.KnowledgeBase.Application.Services;
                public class Handler
                {
                    public Handler(IPdfIndexingPipeline? pipeline) { }
                }
            }
            """;

        var diagnostics = await RunAnalyzerAsync(source);
        AssertNoMaiDiagnostics(diagnostics);
    }

    [Fact]
    public async Task NullablePipelineNullDefaultOnMethodParam_DoesNotFlag()
    {
        // MAI004 only fires on constructor parameters — not regular methods.
        var source = SharedShimSource + """

            #nullable enable
            namespace SubjectUnderTest
            {
                using Api.BoundedContexts.KnowledgeBase.Application.Services;
                public class C
                {
                    public void M(IPdfIndexingPipeline? pipeline = null) { }
                }
            }
            """;

        var diagnostics = await RunAnalyzerAsync(source);
        AssertNoMaiDiagnostics(diagnostics);
    }

    [Fact]
    public async Task UnrelatedNullableInterfaceWithNullDefault_DoesNotFlag()
    {
        var source = SharedShimSource + """

            #nullable enable
            namespace SubjectUnderTest
            {
                public interface ISomethingElse { }
                public class Handler
                {
                    public Handler(ISomethingElse? other = null) { }
                }
            }
            """;

        var diagnostics = await RunAnalyzerAsync(source);
        AssertNoMaiDiagnostics(diagnostics);
    }

    // --------------------------------------------------------------------------
    // MAI005 — new VectorDocumentEntity { ... } outside KnowledgeBaseMappers
    // --------------------------------------------------------------------------

    [Fact]
    public async Task NewVectorDocumentEntity_InCommandHandler_ReportsMAI005()
    {
        var source = SharedShimSource + """

            namespace Api.BoundedContexts.DocumentProcessing.Application.Commands
            {
                using Api.Infrastructure.Entities;
                public class UploadPdfCommandHandler
                {
                    public VectorDocumentEntity Build()
                    {
                        return new VectorDocumentEntity { Id = System.Guid.NewGuid() };
                    }
                }
            }
            """;

        var diagnostics = await RunAnalyzerAsync(source);
        var mai005 = OnlyMai005(diagnostics);

        Assert.Single(mai005);
    }

    [Fact]
    public async Task NewVectorDocumentEntity_InApplicationServices_ReportsMAI005()
    {
        var source = SharedShimSource + """

            namespace Api.BoundedContexts.DocumentProcessing.Application.Services
            {
                using Api.Infrastructure.Entities;
                public class PdfProcessingPipelineService
                {
                    public VectorDocumentEntity Build()
                    {
                        return new VectorDocumentEntity();
                    }
                }
            }
            """;

        var diagnostics = await RunAnalyzerAsync(source);
        var mai005 = OnlyMai005(diagnostics);

        Assert.Single(mai005);
    }

    [Fact]
    public async Task NewVectorDocumentEntity_InsideKnowledgeBaseMappers_DoesNotFlag()
    {
        // The mapper itself is the legitimate construction site. The shared shim
        // already contains a `new VectorDocumentEntity { ... }` inside
        // KnowledgeBaseMappers.ToEntity — running the analyzer over it must NOT
        // produce MAI005.
        var source = SharedShimSource;

        var diagnostics = await RunAnalyzerAsync(source);
        AssertNoMaiDiagnostics(diagnostics);
    }

    [Fact]
    public async Task NewVectorDocumentEntity_InsidePdfIndexingPipeline_DoesNotFlag()
    {
        // PdfIndexingPipeline is the second legitimate construction site (the
        // single-owner ingestion pipeline). Documented in the analyzer's
        // AllowedConstructionSites allowlist.
        var source = SharedShimSource + """

            namespace Api.BoundedContexts.KnowledgeBase.Application.Services
            {
                using Api.Infrastructure.Entities;
                public sealed class PdfIndexingPipeline
                {
                    public VectorDocumentEntity Build()
                    {
                        return new VectorDocumentEntity { Id = System.Guid.NewGuid() };
                    }
                }
            }
            """;

        var diagnostics = await RunAnalyzerAsync(source);
        AssertNoMaiDiagnostics(diagnostics);
    }

    [Fact]
    public async Task UnrelatedTypeCreation_DoesNotFlag()
    {
        var source = SharedShimSource + """

            namespace SubjectUnderTest
            {
                public sealed class OtherEntity { }
                public class Builder
                {
                    public OtherEntity Build() => new OtherEntity();
                }
            }
            """;

        var diagnostics = await RunAnalyzerAsync(source);
        AssertNoMaiDiagnostics(diagnostics);
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

        var compileDiagnostics = compilation.GetDiagnostics();
        var compileErrors = compileDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToImmutableArray();
        if (!compileErrors.IsEmpty)
        {
            throw new InvalidOperationException(
                "Inline test source did not compile cleanly. Errors:\n  " +
                string.Join("\n  ", compileErrors.Select(d => d.ToString())));
        }

        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new NoVectorDocumentBypassAnalyzer());
        var withAnalyzers = compilation.WithAnalyzers(analyzers);
        return await withAnalyzers.GetAnalyzerDiagnosticsAsync().ConfigureAwait(false);
    }

    private static ImmutableArray<Diagnostic> OnlyMai004(ImmutableArray<Diagnostic> diagnostics) =>
        diagnostics.Where(d => d.Id == NoVectorDocumentBypassAnalyzer.NullablePipelineDiagnosticId).ToImmutableArray();

    private static ImmutableArray<Diagnostic> OnlyMai005(ImmutableArray<Diagnostic> diagnostics) =>
        diagnostics.Where(d => d.Id == NoVectorDocumentBypassAnalyzer.EntityBypassDiagnosticId).ToImmutableArray();

    private static void AssertNoMaiDiagnostics(ImmutableArray<Diagnostic> diagnostics)
    {
        var allMai = diagnostics
            .Where(d => d.Id == NoVectorDocumentBypassAnalyzer.NullablePipelineDiagnosticId ||
                        d.Id == NoVectorDocumentBypassAnalyzer.EntityBypassDiagnosticId)
            .ToImmutableArray();

        Assert.Empty(allMai);
    }
}
