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
        // Tier 1 — typed PII value objects. FQN must match the Tier1PiiTypes
        // hashset in NoPiiInLogAnalyzer.
        namespace Api.BoundedContexts.Authentication.Domain.ValueObjects
        {
            public sealed class Email { public string Value => string.Empty; }
            public sealed class PasswordHash { public string Value => string.Empty; }
            public sealed class SessionToken { public string Value => string.Empty; }
            public sealed class TotpSecret { public string Value => string.Empty; }
            public sealed class BackupCode { public string Value => string.Empty; }
        }
        // A non-PII VO used to assert MAI002 does not fire on unrelated types.
        namespace Api.SharedKernel.Domain.ValueObjects
        {
            public sealed class Role { public string Value => string.Empty; }
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

    // -------------------------------------------------------------------------
    // Phase 2a — Tier 1 (typed PII value object detection, MAI002)
    // -------------------------------------------------------------------------

    /// <summary>Positive: Email VO logged unwrapped → MAI002.</summary>
    [Fact]
    public async Task TypedVo_Email_UnwrappedArg_ReportsMAI002()
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
                        logger.LogInformation("Sign-in attempt: {Subject}", email);
                    }
                }
            }
            """;

        var diagnostics = await RunAnalyzerAsync(source).ConfigureAwait(false);
        AssertSingleTypedVoDiagnostic(diagnostics, "Api.BoundedContexts.Authentication.Domain.ValueObjects.Email");
    }

    /// <summary>Positive: PasswordHash VO logged unwrapped → MAI002.</summary>
    [Fact]
    public async Task TypedVo_PasswordHash_UnwrappedArg_ReportsMAI002()
    {
        var source = SharedShimSource + """

            namespace SubjectUnderTest
            {
                using Microsoft.Extensions.Logging;
                using Api.BoundedContexts.Authentication.Domain.ValueObjects;
                public class C
                {
                    public void M(ILogger logger, PasswordHash hash)
                    {
                        logger.LogError("Auth failed for hash {HashRepr}", hash);
                    }
                }
            }
            """;

        var diagnostics = await RunAnalyzerAsync(source).ConfigureAwait(false);
        AssertSingleTypedVoDiagnostic(diagnostics, "Api.BoundedContexts.Authentication.Domain.ValueObjects.PasswordHash");
    }

    /// <summary>Positive: SessionToken VO logged unwrapped → MAI002.</summary>
    [Fact]
    public async Task TypedVo_SessionToken_UnwrappedArg_ReportsMAI002()
    {
        var source = SharedShimSource + """

            namespace SubjectUnderTest
            {
                using Microsoft.Extensions.Logging;
                using Api.BoundedContexts.Authentication.Domain.ValueObjects;
                public class C
                {
                    public void M(ILogger logger, SessionToken token)
                    {
                        logger.LogDebug("Issued session {Subject}", token);
                    }
                }
            }
            """;

        var diagnostics = await RunAnalyzerAsync(source).ConfigureAwait(false);
        AssertSingleTypedVoDiagnostic(diagnostics, "Api.BoundedContexts.Authentication.Domain.ValueObjects.SessionToken");
    }

    /// <summary>Negative: Email VO wrapped via DataMasking.MaskEmail(email.Value) does NOT flag.</summary>
    [Fact]
    public async Task TypedVo_Email_WrappedViaValueAccess_DoesNotFlag()
    {
        var source = SharedShimSource + """

            namespace SubjectUnderTest
            {
                using Microsoft.Extensions.Logging;
                using Api.BoundedContexts.Authentication.Domain.ValueObjects;
                using Api.Infrastructure.Security;
                public class C
                {
                    public void M(ILogger logger, Email email)
                    {
                        logger.LogInformation("Sign-in attempt: {Subject}", DataMasking.MaskEmail(email.Value));
                    }
                }
            }
            """;

        var diagnostics = await RunAnalyzerAsync(source).ConfigureAwait(false);
        AssertNoMaiDiagnostics(diagnostics);
    }

    /// <summary>Negative: non-PII VO (Role) logged unwrapped does NOT flag MAI002.</summary>
    [Fact]
    public async Task TypedVo_NonPiiRole_UnwrappedArg_DoesNotFlag()
    {
        var source = SharedShimSource + """

            namespace SubjectUnderTest
            {
                using Microsoft.Extensions.Logging;
                using Api.SharedKernel.Domain.ValueObjects;
                public class C
                {
                    public void M(ILogger logger, Role role)
                    {
                        logger.LogInformation("Role assigned: {Role}", role);
                    }
                }
            }
            """;

        var diagnostics = await RunAnalyzerAsync(source).ConfigureAwait(false);
        AssertNoMaiDiagnostics(diagnostics);
    }

    // -------------------------------------------------------------------------
    // Phase 2b — Tier 2 (identifier name heuristic, MAI003)
    // -------------------------------------------------------------------------

    /// <summary>Positive: parameter named 'password' logged unwrapped → MAI003.</summary>
    [Fact]
    public async Task HeuristicName_Password_UnwrappedArg_ReportsMAI003()
    {
        var source = SharedShimSource + """

            namespace SubjectUnderTest
            {
                using Microsoft.Extensions.Logging;
                public class C
                {
                    public void M(ILogger logger, string password)
                    {
                        logger.LogWarning("Validating credentials: {Credential}", password);
                    }
                }
            }
            """;

        var diagnostics = await RunAnalyzerAsync(source).ConfigureAwait(false);
        AssertSingleHeuristicNameDiagnostic(diagnostics, "password");
    }

    /// <summary>Positive: parameter named 'accessToken' (camelCase) → MAI003.</summary>
    [Fact]
    public async Task HeuristicName_AccessToken_UnwrappedArg_ReportsMAI003()
    {
        var source = SharedShimSource + """

            namespace SubjectUnderTest
            {
                using Microsoft.Extensions.Logging;
                public class C
                {
                    public void M(ILogger logger, string accessToken)
                    {
                        logger.LogDebug("Issued credential: {Credential}", accessToken);
                    }
                }
            }
            """;

        var diagnostics = await RunAnalyzerAsync(source).ConfigureAwait(false);
        AssertSingleHeuristicNameDiagnostic(diagnostics, "accessToken");
    }

    /// <summary>Negative: parameter named 'sessionId' (not in PII heuristic set) does NOT flag.</summary>
    [Fact]
    public async Task HeuristicName_SessionId_UnwrappedArg_DoesNotFlag()
    {
        var source = SharedShimSource + """

            namespace SubjectUnderTest
            {
                using Microsoft.Extensions.Logging;
                public class C
                {
                    public void M(ILogger logger, string sessionId)
                    {
                        logger.LogInformation("Session active: {SessionId}", sessionId);
                    }
                }
            }
            """;

        var diagnostics = await RunAnalyzerAsync(source).ConfigureAwait(false);
        AssertNoMaiDiagnostics(diagnostics);
    }

    /// <summary>Negative: param 'phoneNumber' wrapped via DataMasking.MaskString does NOT flag.</summary>
    [Fact]
    public async Task HeuristicName_PhoneNumber_Wrapped_DoesNotFlag()
    {
        var source = SharedShimSource + """

            namespace SubjectUnderTest
            {
                using Microsoft.Extensions.Logging;
                using Api.Infrastructure.Security;
                public class C
                {
                    public void M(ILogger logger, string phoneNumber)
                    {
                        logger.LogInformation("SMS target: {Subject}", DataMasking.MaskString(phoneNumber));
                    }
                }
            }
            """;

        var diagnostics = await RunAnalyzerAsync(source).ConfigureAwait(false);
        AssertNoMaiDiagnostics(diagnostics);
    }

    // -------------------------------------------------------------------------
    // Tier priority — Tier 3 (placeholder) wins over Tier 1 / Tier 2
    // -------------------------------------------------------------------------

    /// <summary>
    /// When the placeholder is PII-suggesting AND the argument identifier name is also
    /// PII-suggesting, only MAI001 (Tier 3) should fire — the placeholder signal is the
    /// most actionable cue for the developer.
    /// </summary>
    [Fact]
    public async Task TierPriority_PlaceholderAndIdentifierBothPii_ReportsMAI001Only()
    {
        var source = SharedShimSource + """

            namespace SubjectUnderTest
            {
                using Microsoft.Extensions.Logging;
                public class C
                {
                    public void M(ILogger logger, string email)
                    {
                        logger.LogInformation("Sign-in: {Email}", email);
                    }
                }
            }
            """;

        var diagnostics = await RunAnalyzerAsync(source).ConfigureAwait(false);
        var mai001 = diagnostics.Where(d => d.Id == NoPiiInLogAnalyzer.DiagnosticId).ToImmutableArray();
        var mai002 = diagnostics.Where(d => d.Id == NoPiiInLogAnalyzer.TypedVoDiagnosticId).ToImmutableArray();
        var mai003 = diagnostics.Where(d => d.Id == NoPiiInLogAnalyzer.HeuristicNameDiagnosticId).ToImmutableArray();

        Assert.Single(mai001);
        Assert.Empty(mai002);
        Assert.Empty(mai003);
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
        var allMai = diagnostics
            .Where(d => d.Id is NoPiiInLogAnalyzer.DiagnosticId
                or NoPiiInLogAnalyzer.TypedVoDiagnosticId
                or NoPiiInLogAnalyzer.HeuristicNameDiagnosticId)
            .ToImmutableArray();

        Assert.Empty(allMai);
    }

    private static void AssertSingleTypedVoDiagnostic(ImmutableArray<Diagnostic> diagnostics, string expectedTypeFqn)
    {
        var hits = diagnostics
            .Where(d => string.Equals(d.Id, NoPiiInLogAnalyzer.TypedVoDiagnosticId, StringComparison.Ordinal))
            .ToImmutableArray();

        Assert.Single(hits);
        Assert.Contains(expectedTypeFqn, hits[0].GetMessage());
    }

    private static void AssertSingleHeuristicNameDiagnostic(ImmutableArray<Diagnostic> diagnostics, string expectedIdentifier)
    {
        var hits = diagnostics
            .Where(d => string.Equals(d.Id, NoPiiInLogAnalyzer.HeuristicNameDiagnosticId, StringComparison.Ordinal))
            .ToImmutableArray();

        Assert.Single(hits);
        Assert.Contains($"'{expectedIdentifier}'", hits[0].GetMessage());
    }
}
