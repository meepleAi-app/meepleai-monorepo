using Api.BoundedContexts.SharedGameCatalog.Application.Commands.DeleteGameTranslation;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Commands.DeleteGameTranslation;

/// <summary>
/// Unit tests for <see cref="DeleteGameTranslationCommandValidator"/>.
/// Issue #2339 — sub-PR 1/3 Wave 4 (Task 11).
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class DeleteGameTranslationCommandValidatorTests
{
    private readonly DeleteGameTranslationCommandValidator _sut = new();

    private static DeleteGameTranslationCommand ValidCommand() => new(
        GameId: Guid.NewGuid(),
        Locale: "it",
        Xmin: 12345u,
        ActorUserId: Guid.NewGuid());

    [Fact]
    public async Task Valid_NoErrors()
    {
        var result = await _sut.TestValidateAsync(ValidCommand());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task EmptyGameId_400()
    {
        var cmd = ValidCommand() with { GameId = Guid.Empty };
        var result = await _sut.TestValidateAsync(cmd);
        result.ShouldHaveValidationErrorFor(c => c.GameId);
    }

    [Fact]
    public async Task EmptyActorUserId_400()
    {
        var cmd = ValidCommand() with { ActorUserId = Guid.Empty };
        var result = await _sut.TestValidateAsync(cmd);
        result.ShouldHaveValidationErrorFor(c => c.ActorUserId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("english")]
    [InlineData("xx-yy-zz")]
    public async Task InvalidLocale_400(string locale)
    {
        var cmd = ValidCommand() with { Locale = locale };
        var result = await _sut.TestValidateAsync(cmd);
        result.ShouldHaveValidationErrorFor(c => c.Locale);
    }

    [Fact]
    public async Task XminZero_400()
    {
        var cmd = ValidCommand() with { Xmin = 0u };
        var result = await _sut.TestValidateAsync(cmd);
        result.ShouldHaveValidationErrorFor(c => c.Xmin);
    }
}
