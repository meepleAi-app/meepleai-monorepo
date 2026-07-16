using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.BoundedContexts.UserNotifications.Infrastructure.Email;
using Api.BoundedContexts.UserNotifications.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Api.Tests.BoundedContexts.UserNotifications.Infrastructure.Email;

/// <summary>
/// Unit tests for the email builder layer (issue #3026): the generic MVP builder and the factory
/// fallback resolution. Mirrors the Slack builder tests.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "UserNotifications")]
public class EmailMessageBuilderTests
{
    private static GenericEmailBuilder CreateBuilder()
    {
        var templateService = new EmailTemplateService(new ConfigurationBuilder().Build());
        return new GenericEmailBuilder(templateService);
    }

    [Fact]
    public void BuildMessage_GenericPayload_UsesFriendlyTitleAsSubject_AndPayloadBody_WithCta()
    {
        // Arrange
        var builder = CreateBuilder();
        var context = new EmailBuildContext(
            NotificationType.ShareRequestCreated,
            new GenericPayload("ignored-title", "Please review the pending share request."),
            "/share-requests/9",
            "Alice");

        // Act
        var message = builder.BuildMessage(context);

        // Assert — subject is the friendly ResolveTitle mapping (not the C# type name)
        message.Subject.Should().Be("Nuova Share Request");
        message.HtmlBody.Should().Contain("Please review the pending share request.");
        message.HtmlBody.Should().Contain("Open in MeepleAI");   // CTA present because deep link supplied
        message.HtmlBody.Should().Contain("Alice");              // greeting uses recipient name
    }

    [Fact]
    public void BuildMessage_TypedPayloadWithoutDeepLink_FallsBackToTitleBody_NoCta_NoTypeDump()
    {
        // Arrange
        var builder = CreateBuilder();
        var context = new EmailBuildContext(
            NotificationType.DocumentReady,
            new PdfProcessingPayload(Guid.NewGuid(), "catan.pdf", "Ready"),
            DeepLinkPath: null,
            RecipientName: "Bob");

        // Act
        var message = builder.BuildMessage(context);

        // Assert
        message.Subject.Should().Be("Documento pronto");
        message.HtmlBody.Should().Contain("Documento pronto");
        message.HtmlBody.Should().NotContain("Open in MeepleAI");      // no deep link => no CTA
        message.HtmlBody.Should().NotContain("PdfProcessingPayload");  // never leak the C# record dump
    }

    [Fact]
    public void CanHandle_AlwaysFalse_UsedOnlyAsFallback()
    {
        var builder = CreateBuilder();
        builder.CanHandle(NotificationType.ShareRequestCreated).Should().BeFalse();
        builder.CanHandle(NotificationType.DocumentReady).Should().BeFalse();
    }

    [Fact]
    public void Factory_WithNoSpecificBuilders_ResolvesGenericFallback()
    {
        // Arrange
        var generic = CreateBuilder();
        var factory = new EmailMessageBuilderFactory(Enumerable.Empty<IEmailMessageBuilder>(), generic);

        // Act
        var resolved = factory.GetBuilder(NotificationType.GameNightInvitation);

        // Assert
        resolved.Should().BeSameAs(generic);
    }
}
