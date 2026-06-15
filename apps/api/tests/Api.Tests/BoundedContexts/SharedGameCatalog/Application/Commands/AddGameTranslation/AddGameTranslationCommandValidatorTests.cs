using Api.BoundedContexts.SharedGameCatalog.Application.Commands.AddGameTranslation;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using FluentAssertions;
using FluentValidation.TestHelper;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Commands.AddGameTranslation;

/// <summary>
/// Unit tests for <see cref="AddGameTranslationCommandValidator"/>.
/// Issue #2339 — sub-PR 1/3 Wave 3 (Task 9).
/// </summary>
/// <remarks>
/// Per DEC-C2 (plan 2026-06-15) the game-existence check lives in the handler,
/// not the validator. The validator only checks input shape + the duplicate-locale
/// short-circuit. Game-not-found is therefore tested in
/// <c>AddGameTranslationCommandHandlerTests</c>.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class AddGameTranslationCommandValidatorTests
{
    private readonly Mock<ISharedGameTranslationRepository> _translationRepo = new();
    private readonly AddGameTranslationCommandValidator _sut;

    public AddGameTranslationCommandValidatorTests()
    {
        // Default: no duplicate active translation for any (gameId, locale) pair.
        _translationRepo
            .Setup(r => r.ExistsActiveAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _sut = new AddGameTranslationCommandValidator(_translationRepo.Object);
    }

    [Fact]
    public async Task Valid_NoErrors()
    {
        var cmd = new AddGameTranslationCommand(
            GameId: Guid.NewGuid(),
            Locale: "it",
            Title: "I Coloni di Catan",
            Description: null,
            Source: "manual",
            ActorUserId: Guid.NewGuid());

        var result = await _sut.TestValidateAsync(cmd);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task InvalidLocale_FailsLocaleRule()
    {
        var cmd = new AddGameTranslationCommand(
            GameId: Guid.NewGuid(),
            Locale: "english", // invalid ISO 639-1
            Title: "Foo",
            Description: null,
            Source: "manual",
            ActorUserId: Guid.NewGuid());

        var result = await _sut.TestValidateAsync(cmd);

        result.ShouldHaveValidationErrorFor(c => c.Locale);
    }

    [Fact]
    public async Task EmptyTitle_FailsTitleRule()
    {
        var cmd = new AddGameTranslationCommand(
            GameId: Guid.NewGuid(),
            Locale: "it",
            Title: "",
            Description: null,
            Source: "manual",
            ActorUserId: Guid.NewGuid());

        var result = await _sut.TestValidateAsync(cmd);

        result.ShouldHaveValidationErrorFor(c => c.Title);
    }

    [Fact]
    public async Task InvalidSource_FailsSourceRule()
    {
        var cmd = new AddGameTranslationCommand(
            GameId: Guid.NewGuid(),
            Locale: "it",
            Title: "Foo",
            Description: null,
            Source: "facebook", // not in {manual, auto-openrouter, community}
            ActorUserId: Guid.NewGuid());

        var result = await _sut.TestValidateAsync(cmd);

        result.ShouldHaveValidationErrorFor(c => c.Source);
    }

    [Fact]
    public async Task DuplicateActiveLocale_FailsLocaleRule()
    {
        // Repo reports an existing active translation for ("...", "it")
        var gameId = Guid.NewGuid();
        _translationRepo
            .Setup(r => r.ExistsActiveAsync(gameId, "it", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var cmd = new AddGameTranslationCommand(
            GameId: gameId,
            Locale: "it",
            Title: "Foo",
            Description: null,
            Source: "manual",
            ActorUserId: Guid.NewGuid());

        var result = await _sut.TestValidateAsync(cmd);

        // FluentValidation's WithErrorMessage does a literal match — use a substring check
        // via the underlying Errors collection instead so we don't couple the test to the
        // exact phrasing.
        var localeErrors = result.ShouldHaveValidationErrorFor(c => c.Locale);
        localeErrors.Should().Contain(err => err.ErrorMessage.Contains("already exists", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task EmptyGameId_FailsGameIdRule()
    {
        var cmd = new AddGameTranslationCommand(
            GameId: Guid.Empty,
            Locale: "it",
            Title: "Foo",
            Description: null,
            Source: "manual",
            ActorUserId: Guid.NewGuid());

        var result = await _sut.TestValidateAsync(cmd);

        result.ShouldHaveValidationErrorFor(c => c.GameId);
    }
}
