using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Api.Analyzers.Tests;

/// <summary>
/// Snapshot tests for <see cref="NoPiiInLogCodeFixProvider"/>. For each diagnostic
/// kind (MAI001/MAI002/MAI003) we invoke the fix and assert two invariants on the
/// rewritten source:
///   1. The expected <c>DataMasking.Mask*</c> call appears.
///   2. The <c>using Api.Infrastructure.Security;</c> directive is present.
///
/// We assert by substring rather than by exact diff to avoid coupling tests to
/// trivia formatting (whitespace / line endings vary by Roslyn version).
/// </summary>
public sealed class NoPiiInLogCodeFixProviderTests
{
    private const string SharedShimSource = """
        namespace Microsoft.Extensions.Logging
        {
            public interface ILogger { }
            public static class LoggerExtensions
            {
                public static void LogInformation(this ILogger logger, string message, params object[] args) { }
                public static void LogWarning(this ILogger logger, string message, params object[] args) { }
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
        namespace Api.BoundedContexts.Authentication.Domain.ValueObjects
        {
            public sealed class Email { public string Value => string.Empty; }
        }
        """;

    [Fact]
    public async Task Mai001_EmailPlaceholder_WrapsWithMaskEmail()
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

        var fixedSource = await ApplyCodeFixAsync(source).ConfigureAwait(false);

        Assert.Contains("DataMasking.MaskEmail(userEmail)", fixedSource);
        Assert.Contains("using Api.Infrastructure.Security;", fixedSource);
    }

    [Fact]
    public async Task Mai001_TokenPlaceholder_WrapsWithMaskJwt()
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

        var fixedSource = await ApplyCodeFixAsync(source).ConfigureAwait(false);

        Assert.Contains("DataMasking.MaskJwt(accessToken)", fixedSource);
    }

    [Fact]
    public async Task Mai001_IpAddressPlaceholder_WrapsWithMaskIpAddress()
    {
        var source = SharedShimSource + """

            namespace SubjectUnderTest
            {
                using Microsoft.Extensions.Logging;
                public class C
                {
                    public void M(ILogger logger, string clientIpAddress)
                    {
                        logger.LogInformation("Request from {IpAddress}", clientIpAddress);
                    }
                }
            }
            """;

        var fixedSource = await ApplyCodeFixAsync(source).ConfigureAwait(false);

        Assert.Contains("DataMasking.MaskIpAddress(clientIpAddress)", fixedSource);
    }

    [Fact]
    public async Task Mai002_EmailVo_AppendsValueAccessor()
    {
        var source = SharedShimSource + """

            namespace SubjectUnderTest
            {
                using Microsoft.Extensions.Logging;
                using Api.BoundedContexts.Authentication.Domain.ValueObjects;
                public class C
                {
                    public void M(ILogger logger, Email email)
                    {
                        logger.LogInformation("Sign-in: {Subject}", email);
                    }
                }
            }
            """;

        var fixedSource = await ApplyCodeFixAsync(source).ConfigureAwait(false);

        // VO-typed args get `.Value` appended so the string-typed Mask method accepts them.
        Assert.Contains("DataMasking.MaskEmail(email.Value)", fixedSource);
    }

    [Fact]
    public async Task Mai003_PasswordIdentifier_WrapsWithMaskString()
    {
        var source = SharedShimSource + """

            namespace SubjectUnderTest
            {
                using Microsoft.Extensions.Logging;
                public class C
                {
                    public void M(ILogger logger, string password)
                    {
                        logger.LogWarning("Validating: {Credential}", password);
                    }
                }
            }
            """;

        var fixedSource = await ApplyCodeFixAsync(source).ConfigureAwait(false);

        Assert.Contains("DataMasking.MaskString(password)", fixedSource);
    }

    [Fact]
    public async Task CodeFix_IsRegisteredForAllThreeDiagnosticIds()
    {
        var fixProvider = new NoPiiInLogCodeFixProvider();
        var fixable = fixProvider.FixableDiagnosticIds;

        Assert.Contains(NoPiiInLogAnalyzer.DiagnosticId, fixable);
        Assert.Contains(NoPiiInLogAnalyzer.TypedVoDiagnosticId, fixable);
        Assert.Contains(NoPiiInLogAnalyzer.HeuristicNameDiagnosticId, fixable);
    }

    private static async Task<string> ApplyCodeFixAsync(string source)
    {
        var workspace = new Microsoft.CodeAnalysis.AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var documentId = DocumentId.CreateNewId(projectId);

        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Runtime.CompilerServices.RuntimeHelpers).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location),
        };

        var solution = workspace.CurrentSolution
            .AddProject(projectId, "TestProject", "TestProject", LanguageNames.CSharp)
            .WithProjectCompilationOptions(projectId,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReferences(projectId, references)
            .AddDocument(documentId, "Test.cs", source);

        var document = solution.GetDocument(documentId)!;
        var compilation = await document.Project.GetCompilationAsync().ConfigureAwait(false);
        Assert.NotNull(compilation);

        var compileErrors = compilation!.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToImmutableArray();
        if (!compileErrors.IsEmpty)
        {
            throw new InvalidOperationException(
                "Inline test source did not compile cleanly. Errors:\n  " +
                string.Join("\n  ", compileErrors.Select(d => d.ToString())));
        }

        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new NoPiiInLogAnalyzer());
        var diagnostics = await compilation.WithAnalyzers(analyzers)
            .GetAnalyzerDiagnosticsAsync(CancellationToken.None)
            .ConfigureAwait(false);

        var maiDiagnostic = diagnostics.FirstOrDefault(d =>
            d.Id == NoPiiInLogAnalyzer.DiagnosticId
            || d.Id == NoPiiInLogAnalyzer.TypedVoDiagnosticId
            || d.Id == NoPiiInLogAnalyzer.HeuristicNameDiagnosticId);
        Assert.NotNull(maiDiagnostic);

        var fixProvider = new NoPiiInLogCodeFixProvider();
        CodeAction? registeredAction = null;
        var context = new CodeFixContext(
            document,
            maiDiagnostic!,
            (action, _) => registeredAction = action,
            CancellationToken.None);

        await fixProvider.RegisterCodeFixesAsync(context).ConfigureAwait(false);
        Assert.NotNull(registeredAction);

        var operations = await registeredAction!.GetOperationsAsync(CancellationToken.None).ConfigureAwait(false);
        var applyChanges = operations.OfType<ApplyChangesOperation>().Single();
        var newDocument = applyChanges.ChangedSolution.GetDocument(documentId)!;
        var newRoot = await newDocument.GetSyntaxRootAsync().ConfigureAwait(false);

        return newRoot!.ToFullString();
    }
}
