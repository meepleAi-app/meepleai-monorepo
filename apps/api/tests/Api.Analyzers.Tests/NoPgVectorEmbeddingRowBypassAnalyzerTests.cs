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
/// Test di <see cref="NoPgVectorEmbeddingRowBypassAnalyzer"/> (MAI007, #4384): una riga di
/// <c>pgvector_embeddings</c> nasce solo da <c>PgVectorEmbeddingEntity.ForChunk</c>, che chiede il
/// collegamento al passaggio. L'analizzatore confronta il nome completo del tipo, quindi lo shim usa
/// quello di produzione.
/// </summary>
public sealed class NoPgVectorEmbeddingRowBypassAnalyzerTests
{
    private const string EntityShim = """
        namespace Api.Infrastructure.Entities.KnowledgeBase
        {
            public sealed record TextChunkLink(System.Guid TextChunkId, int RoleTags);

            public class PgVectorEmbeddingEntity
            {
                public System.Guid Id { get; set; }
                public System.Guid? SourceChunkId { get; set; }

                // Il costruttore ammesso: MAI007 non deve scattare qui.
                public static PgVectorEmbeddingEntity ForChunk(TextChunkLink? sourceChunk) => new()
                {
                    Id = System.Guid.NewGuid(),
                    SourceChunkId = sourceChunk?.TextChunkId,
                };
            }
        }
        """;

    [Fact]
    public async Task ExplicitConstruction_InAWriter_ReportsMAI007()
    {
        var diagnostics = await RunAnalyzerAsync(EntityShim + """

            namespace SubjectUnderTest
            {
                using Api.Infrastructure.Entities.KnowledgeBase;
                public class Writer
                {
                    public PgVectorEmbeddingEntity Build() => new PgVectorEmbeddingEntity { Id = System.Guid.NewGuid() };
                }
            }
            """);

        Assert.Single(OnlyMai007(diagnostics));
    }

    [Fact]
    public async Task FullyQualifiedConstruction_ReportsMAI007()
    {
        var diagnostics = await RunAnalyzerAsync(EntityShim + """

            namespace SubjectUnderTest
            {
                public class Writer
                {
                    public object Build() => new Api.Infrastructure.Entities.KnowledgeBase.PgVectorEmbeddingEntity();
                }
            }
            """);

        Assert.Single(OnlyMai007(diagnostics));
    }

    [Fact]
    public async Task TargetTypedConstruction_ReportsMAI007()
    {
        // La forma che una scansione del testo non vede.
        var diagnostics = await RunAnalyzerAsync(EntityShim + """

            namespace SubjectUnderTest
            {
                using Api.Infrastructure.Entities.KnowledgeBase;
                public class Writer
                {
                    public PgVectorEmbeddingEntity Build()
                    {
                        PgVectorEmbeddingEntity row = new() { Id = System.Guid.NewGuid() };
                        return row;
                    }
                }
            }
            """);

        Assert.Single(OnlyMai007(diagnostics));
    }

    [Fact]
    public async Task ForChunk_IsTheAllowedConstructionSite()
    {
        var diagnostics = await RunAnalyzerAsync(EntityShim + """

            namespace SubjectUnderTest
            {
                using Api.Infrastructure.Entities.KnowledgeBase;
                public class Writer
                {
                    public PgVectorEmbeddingEntity Build() =>
                        PgVectorEmbeddingEntity.ForChunk(new TextChunkLink(System.Guid.NewGuid(), 2));
                }
            }
            """);

        Assert.Empty(OnlyMai007(diagnostics));
    }

    [Fact]
    public async Task UnrelatedTypeCreation_DoesNotFlag()
    {
        var diagnostics = await RunAnalyzerAsync(EntityShim + """

            namespace SubjectUnderTest
            {
                public sealed class OtherEntity { }
                public class Writer
                {
                    public OtherEntity Build() => new();
                }
            }
            """);

        Assert.Empty(OnlyMai007(diagnostics));
    }

    private static async Task<ImmutableArray<Diagnostic>> RunAnalyzerAsync(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest));
        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Runtime.CompilerServices.RuntimeHelpers).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location),
        };

        var compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: [syntaxTree],
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var compileErrors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToImmutableArray();
        if (!compileErrors.IsEmpty)
        {
            throw new InvalidOperationException(
                "Inline test source did not compile cleanly. Errors:\n  " +
                string.Join("\n  ", compileErrors.Select(d => d.ToString())));
        }

        var withAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(new NoPgVectorEmbeddingRowBypassAnalyzer()));
        return await withAnalyzers.GetAnalyzerDiagnosticsAsync().ConfigureAwait(false);
    }

    private static ImmutableArray<Diagnostic> OnlyMai007(ImmutableArray<Diagnostic> diagnostics) =>
        diagnostics.Where(d => d.Id == NoPgVectorEmbeddingRowBypassAnalyzer.DiagnosticId).ToImmutableArray();
}
