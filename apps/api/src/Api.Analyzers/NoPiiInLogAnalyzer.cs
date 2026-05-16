using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Api.Analyzers;

/// <summary>
/// MAI001 — flags <c>ILogger.Log*</c> calls whose message template contains a
/// PII-suggesting placeholder (e.g. <c>{Email}</c>, <c>{Token}</c>) and whose
/// corresponding argument is NOT wrapped via the canonical PII masking sanitizer
/// <c>Api.Infrastructure.Security.DataMasking.Mask*</c>.
///
/// ADR-058 distinguishes two orthogonal concerns:
///   - Integrity (log-forging, CWE-117) → <c>Api.Helpers.LogSanitizer.Sanitize</c>
///   - Confidentiality (PII exposure, CWE-359) → <c>Api.Infrastructure.Security.DataMasking.Mask*</c>
///
/// Phase 1 (this PR) implements Tier 3 detection (placeholder name match). Tiers 1 (typed VOs)
/// and 2 (parameter name heuristic) are deferred to future PRs per issue #1197.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NoPiiInLogAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MAI001";

    private const string Title = "PII argument logged without canonical masking";
    private const string MessageFormat = "Log placeholder '{{{0}}}' suggests PII content; wrap argument with DataMasking.Mask* (ADR-058)";
    private const string Category = "Security";
    private const string Description =
        "Per ADR-058 / docs/for-developers/security/pii-safe-logging.md, PII fields " +
        "(email, password, token, phone, SSN, IP address) must be masked via " +
        "Api.Infrastructure.Security.DataMasking.Mask* before being logged. " +
        "Note: Api.Helpers.LogSanitizer.Sanitize is for log-forging integrity (CWE-117) " +
        "and is NOT a valid PII masker (CWE-359).";
    private const string HelpLinkUri = "https://github.com/meepleAi-app/meepleai-monorepo/blob/main-dev/docs/for-developers/security/pii-safe-logging.md";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        Title,
        MessageFormat,
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: Description,
        helpLinkUri: HelpLinkUri);

    /// <summary>
    /// Placeholder names that suggest the corresponding argument is PII.
    /// Match is case-insensitive. Extending this list is allowed without an ADR
    /// change as long as the new placeholder is unambiguously PII (no false-positive
    /// risk from non-PII semantics). For ambiguous names see Tier 2 (Phase 2+).
    /// </summary>
    private static readonly ImmutableHashSet<string> PiiPlaceholderNames =
        ImmutableHashSet.CreateRange(StringComparer.OrdinalIgnoreCase, new[]
        {
            "Email",
            "EmailAddress",
            "Password",
            "Token",
            "Jwt",
            "JwtToken",
            "Phone",
            "PhoneNumber",
            "Ssn",
            "IpAddress",
        });

    private const string DataMaskingTypeName = "Api.Infrastructure.Security.DataMasking";
    private const string MaskMethodPrefix = "Mask";

    private const string LoggerExtensionsTypeName = "Microsoft.Extensions.Logging.LoggerExtensions";
    private const string LoggerInterfaceTypeName = "Microsoft.Extensions.Logging.ILogger";

    /// <summary>
    /// Matches log message template placeholders. Microsoft.Extensions.Logging uses
    /// Serilog-style brace placeholders; we accept the same grammar (alphanumeric +
    /// underscore identifier, optionally prefixed by <c>@</c> or <c>$</c> for
    /// destructured / stringified hints which we ignore for matching).
    /// </summary>
    private static readonly Regex PlaceholderRegex = new(
        @"\{[@$]?(?<name>[A-Za-z_][A-Za-z0-9_]*)(?:[,:][^}]*)?\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            return;
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;

        if (!IsLoggerLogCall(method))
        {
            return;
        }

        var templateLiteral = ExtractMessageTemplateLiteral(invocation);
        if (templateLiteral is null)
        {
            return;
        }

        var piiPositions = FindPiiPlaceholderPositions(templateLiteral);
        if (piiPositions.Count == 0)
        {
            return;
        }

        var argValues = ExtractParamsArgumentValues(invocation);
        if (argValues.IsDefaultOrEmpty)
        {
            return;
        }

        foreach (var (name, index) in piiPositions)
        {
            if (index >= argValues.Length)
            {
                continue;
            }

            var argOp = argValues[index];
            if (IsWrappedByDataMasking(argOp))
            {
                continue;
            }

            var location = argOp.Syntax?.GetLocation() ?? invocation.Syntax.GetLocation();
            context.ReportDiagnostic(Diagnostic.Create(Rule, location, name));
        }
    }

    private static bool IsLoggerLogCall(IMethodSymbol method)
    {
        if (method.Name.Length < 4 || !method.Name.StartsWith("Log", StringComparison.Ordinal))
        {
            return false;
        }

        var containingType = method.ContainingType;
        if (containingType is null)
        {
            return false;
        }

        var containingDisplay = containingType.ToDisplayString();
        if (string.Equals(containingDisplay, LoggerExtensionsTypeName, StringComparison.Ordinal))
        {
            return true;
        }

        // Direct ILogger.Log(...) calls (less common but valid).
        if (string.Equals(containingDisplay, LoggerInterfaceTypeName, StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }

    private static string? ExtractMessageTemplateLiteral(IInvocationOperation invocation)
    {
        // LoggerExtensions.Log* signatures consistently expose the template as the
        // parameter named "message". Falling back to any constant-string argument
        // would risk false positives on overloads that take a non-template string.
        foreach (var arg in invocation.Arguments)
        {
            if (arg.Parameter is null)
            {
                continue;
            }

            if (!string.Equals(arg.Parameter.Name, "message", StringComparison.Ordinal))
            {
                continue;
            }

            // The argument value may be wrapped in implicit conversions.
            var value = UnwrapConversions(arg.Value);
            if (value.ConstantValue.HasValue && value.ConstantValue.Value is string template)
            {
                return template;
            }
        }

        return null;
    }

    private static List<(string Name, int Index)> FindPiiPlaceholderPositions(string template)
    {
        var result = new List<(string, int)>();
        var matches = PlaceholderRegex.Matches(template);
        for (var i = 0; i < matches.Count; i++)
        {
            var name = matches[i].Groups["name"].Value;
            if (PiiPlaceholderNames.Contains(name))
            {
                result.Add((name, i));
            }
        }

        return result;
    }

    private static ImmutableArray<IOperation> ExtractParamsArgumentValues(IInvocationOperation invocation)
    {
        foreach (var arg in invocation.Arguments)
        {
            if (arg.Parameter is null || !arg.Parameter.IsParams)
            {
                continue;
            }

            var value = UnwrapConversions(arg.Value);
            if (value is IArrayCreationOperation array)
            {
                return array.Initializer?.ElementValues ?? ImmutableArray<IOperation>.Empty;
            }
        }

        return ImmutableArray<IOperation>.Empty;
    }

    private static bool IsWrappedByDataMasking(IOperation operation)
    {
        var unwrapped = UnwrapConversions(operation);
        if (unwrapped is not IInvocationOperation invocation)
        {
            return false;
        }

        var method = invocation.TargetMethod;
        if (!method.Name.StartsWith(MaskMethodPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var containingType = method.ContainingType?.ToDisplayString();
        return string.Equals(containingType, DataMaskingTypeName, StringComparison.Ordinal);
    }

    private static IOperation UnwrapConversions(IOperation operation)
    {
        var current = operation;
        while (current is IConversionOperation conversion)
        {
            current = conversion.Operand;
        }

        return current;
    }
}
