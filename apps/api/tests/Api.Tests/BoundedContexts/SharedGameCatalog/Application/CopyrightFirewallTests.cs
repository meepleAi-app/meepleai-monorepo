using System.Reflection;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application;

/// <summary>
/// Ensures the Variant C copyright firewall is maintained at the DI level.
/// The AiAssist handler must NEVER have access to PDF storage or blob services.
/// </summary>
public class CopyrightFirewallTests
{
    [Fact]
    public void AiAssistHandler_MustNotInject_BlobOrPdfDependencies()
    {
        var handlerType = typeof(AiAssistMechanicDraftCommandHandler);
        var constructors = handlerType.GetConstructors(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        var parameterTypeNames = constructors
            .SelectMany(c => c.GetParameters())
            .Select(p => p.ParameterType.Name)
            .ToList();

        var forbiddenPatterns = new[]
        {
            "IBlobStorageService",
            "IPdfDocumentRepository",
            "IBlobService",
            "IFileStorageService",
            "IPdfStorage",
        };

        foreach (var forbidden in forbiddenPatterns)
        {
            parameterTypeNames.Should().NotContain(
                name => name.Contains(forbidden, StringComparison.OrdinalIgnoreCase),
                $"AiAssist handler must not depend on {forbidden} — this would violate the Variant C copyright firewall");
        }
    }

    [Fact]
    public void AiAssistHandler_OnlyExpectedDependencies()
    {
        var handlerType = typeof(AiAssistMechanicDraftCommandHandler);
        var constructors = handlerType.GetConstructors(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        var parameterTypeNames = constructors
            .SelectMany(c => c.GetParameters())
            .Select(p => p.ParameterType.Name)
            .ToList();

        // IMechanicDraftRepository is allowed — it accesses the draft, not PDF content
        // IUnitOfWork is allowed — standard persistence pattern
        var allowedPrefixes = new[] { "ILlmService", "IMechanicDraftRepository", "IUnitOfWork", "ILogger" };

        foreach (var param in parameterTypeNames)
        {
            allowedPrefixes.Should().Contain(
                prefix => param.StartsWith(prefix, StringComparison.Ordinal),
                $"Unexpected dependency '{param}' in AiAssist handler — review for copyright compliance");
        }
    }
}
