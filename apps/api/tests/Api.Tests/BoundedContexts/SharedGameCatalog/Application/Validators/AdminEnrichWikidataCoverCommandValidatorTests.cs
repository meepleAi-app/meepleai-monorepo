using Api.BoundedContexts.SharedGameCatalog.Application.Commands.EnrichCatalogCover;
using Api.BoundedContexts.SharedGameCatalog.Application.Validators;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Validators;

/// <summary>
/// Unit tests for <see cref="AdminEnrichWikidataCoverCommandValidator"/>.
/// Issue #1823 Wave 3 M12.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "1823")]
public class AdminEnrichWikidataCoverCommandValidatorTests
{
    private readonly AdminEnrichWikidataCoverCommandValidator _sut = new();

    [Fact]
    public void ValidCommand_PassesValidation()
    {
        var cmd = new AdminEnrichWikidataCoverCommand(
            GameId: Guid.NewGuid(), ForceRefresh: false, TriggeredByUserId: Guid.NewGuid());

        var result = _sut.TestValidate(cmd);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyGameId_FailsValidation()
    {
        var cmd = new AdminEnrichWikidataCoverCommand(
            GameId: Guid.Empty, ForceRefresh: false, TriggeredByUserId: Guid.NewGuid());

        var result = _sut.TestValidate(cmd);

        result.ShouldHaveValidationErrorFor(c => c.GameId);
    }

    [Fact]
    public void EmptyTriggeredByUserId_FailsValidation()
    {
        var cmd = new AdminEnrichWikidataCoverCommand(
            GameId: Guid.NewGuid(), ForceRefresh: false, TriggeredByUserId: Guid.Empty);

        var result = _sut.TestValidate(cmd);

        result.ShouldHaveValidationErrorFor(c => c.TriggeredByUserId);
    }
}
