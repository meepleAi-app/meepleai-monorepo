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
/// Snapshot tests for <see cref="NoPiiInLogAnalyzer"/> (MAI001).
///
/// Each test inlines a self-contained mock of <c>Microsoft.Extensions.Logging</c> and
/// <c>Api.Infrastructure.Security.DataMasking</c> so the suite does not depend on the
/// production assembly. The analyzer matches by fully-qualified type name, so the
/// inline types must use the same FQN as the real ones.
///
/// We invoke the analyzer through <see cref="CompilationWithAnalyzers"/> directly to
/// avoid pulling in the Microsoft.CodeAnalysis.Testing.* packages, whose 1.1.x branch
/// pins Roslyn 1.0.1 and conflicts with our 4.8.0 binding.
/// </summary>
public sealed class NoPiiInLogAnalyzerTests
{
    private const string SharedShimSource = """
        namespace Microsoft.Extensions.Logging
        {
            public interface ILogger { }
            public static class LoggerExtensions
            {
                public static void LogInformation(this ILogger logger, string message, params object[] args) { }
                public static void LogWarning(this ILogger logger, string message, params object[] args) { }
                public static void LogError(this ILogger logger, string message, params object[] args) { }
                public static void LogDebug(this ILogger logger, string message, params object[] args) { }
            }
        }
        namespace Api.Infrastructure.Security
        {
            public static class DataMasking
            {
                public static string MaskEmail(string? value) => string.Empty;
                public static string MaskString(string? value, int maxLength = 4) => string.Empty;
                public static string MaskJwt(string? value) => string.Empty;
                public static string MaskIpAddress(string? value) => string.Empty;
            }
        }
        namespace Api.Helpers
        {
            public static class LogSanitizer
            {
                public static string Sanitize(string? value) => string.Empty;
            }
        }
        """;

    /// <summary>Positive case: unwrapped Email placeholder must flag MAI001.</summary>
    [Fact]
    public async Task EmailPlaceholder_UnwrappedString_ReportsMAI001()
    {
        var source = SharedShimSource + """

            namespace SubjectUnderTest
            {
                using Microsoft.Extensions.Logging;
                public class C
                {
                    public void M(ILogger logger, string userEmail)
                    {
                        logger.LogInformation("User logged in: {Email}", userEmail);
                    }
                }
            }
            """;

        var diagnostics = await RunAnalyzerAsync(source).ConfigureAwait(false);
        AssertSingleMaiDiagnostic(diagnostics, expectedPlaceholderName: "Email");
    }

    /// <summary>Positive case: unwrapped Token placeholder must flag MAI001.</summary>
    [Fact]
    public async Task TokenPlaceholder_UnwrappedString_ReportsMAI001()
    {
        var source = SharedShimSource + """

            namespace SubjectUnderTest
            {
                using Microsoft.Extensions.Logging;
                public class C
                {
                    public void M(ILogger logger, string accessToken)
                    {
                        logger.LogDebug("Issued JWT: {Token}", accessToken);
                    }
                }
            }
            """;

        var diagnostics = await RunAnalyzerAsync(source).ConfigureAwait(false);
        AssertSingleMaiDiagnostic(diagnostics, expectedPlaceholderName: "Token");
    }

    /// <summary>Positive case: unwrapped Phone placeholder must flag MAI001.</summary>
    [Fact]
    public async Task PhonePlaceholder_UnwrappedString_ReportsMAI001()
    {
        var source = SharedShimSource + """

            namespace SubjectUnderTest
            {
                using Microsoft.Extensions.Logging;
                public class C
                {
                    public void M(ILogger logger, string callerPhone)
                    {
                        logger.LogWarning("Verification SMS sent to {Phone}", callerPhone);
                    }
                }
            }
            """;

        var diagnostics = await RunAnalyzerAsync(source).ConfigureAwait(false);
        AssertSingleMaiDiagnostic(diagnostics, expectedPlaceholderName: "Phone");
    }

    /// <summary>Negative case: DataMasking.MaskEmail wrap must NOT flag.</summary>
    [Fact]
    public async Task EmailPlaceholder_WrappedWithMaskEmail_DoesNotFlag()
    {
        var source = SharedShimSource + """

            namespace SubjectUnderTest
            {
                using Microsoft.Extensions.Logging;
                using Api.Infrastructure.Security;
                public class C
                {
                    public void M(ILogger logger, string userEmail)
                    {
                        logger.LogInformation("User logged in: {Email}", DataMasking.MaskEmail(userEmail));
                    }
                }
            }
            """;

        var diagnostics = await RunAnalyzerAsync(source).ConfigureAwait(false);
        AssertNoMaiDiagnostics(diagnostics);
    }

    /// <summary>Negative case: DataMasking.MaskJwt wrap on Token placeholder must NOT flag.</summary>
    [Fact]
    public async Task TokenPlaceholder_WrappedWithMaskJwt_DoesNotFlag()
    {
        var source = SharedShimSource + """

            namespace SubjectUnderTest
            {
                using Microsoft.Extensions.Logging;
                using Api.Infrastructure.Security;
                public class C
                {
                    public void M(ILogger logger, string accessToken)
                    {
                        logger.LogDebug("Issued JWT: {Token}", DataMasking.MaskJwt(accessToken));
                    }
                }
            }
            """;

        var diagnostics = await RunAnalyzerAsync(source).ConfigureAwait(false);
        AssertNoMaiDiagnostics(diagnostics);
    }

    /// <summary>
    /// Negative case: a non-PII placeholder (e.g. {SessionId}) must NOT flag even when
    /// the argument is unwrapped. Phase 1 only watches the curated PII list; SessionId
    /// is intentionally NOT on it (Tier 2/3 will revisit identifier handling).
    /// </summary>
    [Fact]
    public async Task NonPiiPlaceholder_UnwrappedString_DoesNotFlag()
    {
        var source = SharedShimSource + """

            namespace SubjectUnderTest
            {
                using Microsoft.Extensions.Logging;
                public class C
                {
                    public void M(ILogger logger, string sessionId)
                    {
                        logger.LogInformation("Session {SessionId} created", sessionId);
                    }
                }
            }
            """;

        var diagnostics = await RunAnalyzerAsync(source).ConfigureAwait(false);
        AssertNoMaiDiagnostics(diagnostics);
    }

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

        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new NoPiiInLogAnalyzer());
        var withAnalyzers = compilation.WithAnalyzers(analyzers);
        return await withAnalyzers.GetAnalyzerDiagnosticsAsync().ConfigureAwait(false);
    }

    private static void AssertSingleMaiDiagnostic(ImmutableArray<Diagnostic> diagnostics, string expectedPlaceholderName)
    {
        var maiDiagnostics = diagnostics
            .Where(d => string.Equals(d.Id, NoPiiInLogAnalyzer.DiagnosticId, StringComparison.Ordinal))
            .ToImmutableArray();

        Assert.Single(maiDiagnostics);
        Assert.Contains($"'{{{expectedPlaceholderName}}}'", maiDiagnostics[0].GetMessage());
    }

    private static void AssertNoMaiDiagnostics(ImmutableArray<Diagnostic> diagnostics)
    {
        var maiDiagnostics = diagnostics
            .Where(d => string.Equals(d.Id, NoPiiInLogAnalyzer.DiagnosticId, StringComparison.Ordinal))
            .ToImmutableArray();

        Assert.Empty(maiDiagnostics);
    }
}
