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
/// Flags <c>ILogger.Log*</c> calls whose argument is PII-suggesting and is NOT wrapped via
/// the canonical PII masking sanitizer <c>Api.Infrastructure.Security.DataMasking.Mask*</c>.
///
/// Three tiers of detection (in priority order; one diagnostic per argument):
///   - <b>Tier 3 — MAI001</b>: log template placeholder name matches a curated PII set
///     (e.g. <c>{Email}</c>, <c>{Token}</c>). Phase 1.
///   - <b>Tier 1 — MAI002</b>: argument type fully-qualified name matches a known PII
///     value-object list (Email / PasswordHash / SessionToken / TotpSecret / BackupCode).
///     Phase 2a.
///   - <b>Tier 2 — MAI003</b>: argument is an identifier reference (parameter / local /
///     field / property) whose name matches a heuristic PII name set (email, password,
///     token, phone, ssn, ipAddress, …). Phase 2b — highest false-positive risk; severity
///     stays Warning + WarningsNotAsErrors so it does not fail builds.
///
/// ADR-058 distinguishes two orthogonal concerns:
///   - Integrity (log-forging, CWE-117) → <c>Api.Helpers.LogSanitizer.Sanitize</c>
///   - Confidentiality (PII exposure, CWE-359) → <c>Api.Infrastructure.Security.DataMasking.Mask*</c>
///
/// CodeFixProvider auto-wrap (Phase 3) and NuGet packaging (Phase 4) are deferred per #1197.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NoPiiInLogAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MAI001";
    public const string TypedVoDiagnosticId = "MAI002";
    public const string HeuristicNameDiagnosticId = "MAI003";

    private const string Category = "Security";
    private const string HelpLinkUri = "https://github.com/meepleAi-app/meepleai-monorepo/blob/main-dev/docs/for-developers/security/pii-safe-logging.md";
    private const string SharedDescription =
        "Per ADR-058 / docs/for-developers/security/pii-safe-logging.md, PII fields " +
        "(email, password, token, phone, SSN, IP address) must be masked via " +
        "Api.Infrastructure.Security.DataMasking.Mask* before being logged. " +
        "Note: Api.Helpers.LogSanitizer.Sanitize is for log-forging integrity (CWE-117) " +
        "and is NOT a valid PII masker (CWE-359).";

    private static readonly DiagnosticDescriptor Mai001Rule = new(
        DiagnosticId,
        title: "PII argument logged without canonical masking (placeholder match)",
        messageFormat: "Log placeholder '{{{0}}}' suggests PII content; wrap argument with DataMasking.Mask* (ADR-058)",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: SharedDescription,
        helpLinkUri: HelpLinkUri);

    private static readonly DiagnosticDescriptor Mai002Rule = new(
        TypedVoDiagnosticId,
        title: "PII typed value object logged without canonical masking",
        messageFormat: "Argument of type '{0}' is a PII value object; wrap with DataMasking.Mask* before logging (ADR-058)",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: SharedDescription,
        helpLinkUri: HelpLinkUri);

    private static readonly DiagnosticDescriptor Mai003Rule = new(
        HeuristicNameDiagnosticId,
        title: "Likely PII argument logged without canonical masking (name heuristic)",
        messageFormat: "Argument identifier '{0}' likely contains PII; wrap with DataMasking.Mask* (heuristic; if false-positive, rename arg or [SuppressMessage] with justification) (ADR-058)",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: SharedDescription,
        helpLinkUri: HelpLinkUri);

    /// <summary>
    /// Tier 3 — Placeholder names that suggest the corresponding argument is PII.
    /// Match is case-insensitive. Extending this list is allowed without an ADR
    /// change as long as the new placeholder is unambiguously PII (no false-positive
    /// risk from non-PII semantics).
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

    /// <summary>
    /// Tier 1 — Fully-qualified type names whose runtime instances are known PII.
    /// Curated against <c>Api.BoundedContexts.Authentication.Domain.ValueObjects/</c>.
    /// Extending this list requires explicit review (add a new VO only if it
    /// represents PII content, not arbitrary identifiers).
    /// </summary>
    private static readonly ImmutableHashSet<string> Tier1PiiTypes =
        ImmutableHashSet.CreateRange(StringComparer.Ordinal, new[]
        {
            "Api.BoundedContexts.Authentication.Domain.ValueObjects.Email",
            "Api.BoundedContexts.Authentication.Domain.ValueObjects.PasswordHash",
            "Api.BoundedContexts.Authentication.Domain.ValueObjects.SessionToken",
            "Api.BoundedContexts.Authentication.Domain.ValueObjects.TotpSecret",
            "Api.BoundedContexts.Authentication.Domain.ValueObjects.BackupCode",
        });

    /// <summary>
    /// Tier 2 — Identifier names (parameter / local / field / property) that the
    /// heuristic treats as PII. Curated to keep false-positive rate low; if a real
    /// name should match (e.g. <c>jwtSignature</c>) we add it explicitly rather than
    /// loosening the regex. <c>OrdinalIgnoreCase</c> covers both camelCase and PascalCase.
    /// </summary>
    private static readonly ImmutableHashSet<string> Tier2PiiNames =
        ImmutableHashSet.CreateRange(StringComparer.OrdinalIgnoreCase, new[]
        {
            "email", "emailAddress", "userEmail", "user_email",
            "password", "userPassword", "newPassword", "oldPassword", "currentPassword",
            "token", "accessToken", "refreshToken", "apiToken", "authToken",
            "jwt", "jwtToken", "bearerToken",
            "phone", "phoneNumber", "userPhone",
            "ssn",
            "ipAddress", "remoteIp", "clientIp",
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

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Mai001Rule, Mai002Rule, Mai003Rule);

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

        var argValues = ExtractParamsArgumentValues(invocation);
        if (argValues.IsDefaultOrEmpty)
        {
            return;
        }

        var templateLiteral = ExtractMessageTemplateLiteral(invocation);
        var piiPositions = templateLiteral is null
            ? null
            : FindPiiPlaceholderPositions(templateLiteral);

        for (var i = 0; i < argValues.Length; i++)
        {
            var argOp = argValues[i];
            if (IsWrappedByDataMasking(argOp))
            {
                continue;
            }

            // Tier 3 — placeholder name match. Highest priority: when both the
            // template and the argument are PII-suggesting, the template signal is
            // the most actionable for the user.
            var placeholderName = ResolvePiiPlaceholderAt(piiPositions, i);
            if (placeholderName is not null)
            {
                ReportDiagnostic(context, Mai001Rule, argOp, placeholderName);
                continue;
            }

            // Tier 1 — typed PII value object as argument.
            var unwrapped = UnwrapConversions(argOp);
            var typeFqn = unwrapped.Type?.ToDisplayString();
            if (typeFqn is not null && Tier1PiiTypes.Contains(typeFqn))
            {
                ReportDiagnostic(context, Mai002Rule, argOp, typeFqn);
                continue;
            }

            // Tier 2 — identifier name heuristic.
            var identifierName = GetReferencedIdentifierName(unwrapped);
            if (identifierName is not null && Tier2PiiNames.Contains(identifierName))
            {
                ReportDiagnostic(context, Mai003Rule, argOp, identifierName);
            }
        }
    }

    private static void ReportDiagnostic(
        OperationAnalysisContext context,
        DiagnosticDescriptor rule,
        IOperation argOp,
        string messageArg)
    {
        var location = argOp.Syntax?.GetLocation() ?? context.Operation.Syntax.GetLocation();
        context.ReportDiagnostic(Diagnostic.Create(rule, location, messageArg));
    }

    private static string? GetReferencedIdentifierName(IOperation operation)
    {
        return operation switch
        {
            IParameterReferenceOperation parameter => parameter.Parameter.Name,
            ILocalReferenceOperation local => local.Local.Name,
            IFieldReferenceOperation field => field.Field.Name,
            IPropertyReferenceOperation property => property.Property.Name,
            _ => null,
        };
    }

    private static string? ResolvePiiPlaceholderAt(List<(string Name, int Index)>? positions, int argIndex)
    {
        if (positions is null)
        {
            return null;
        }

        foreach (var (name, index) in positions)
        {
            if (index == argIndex)
            {
                return string.IsNullOrEmpty(name) ? null : name;
            }
        }

        return null;
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
