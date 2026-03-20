using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class BulkImportGamesCommandValidatorTests
{
    private readonly BulkImportGamesCommandValidator _validator;

    public BulkImportGamesCommandValidatorTests()
    {
        _validator = new BulkImportGamesCommandValidator();
    }

    [Fact]
    public void Validate_WithValidCommand_PassesValidation()
    {
        var games = new List<BulkGameImportDto>
        {
            new BulkGameImportDto(123, null, null, null, null, null, null, null),
            new BulkGameImportDto(null, "Manual Game", 2020, "Description", 2, 4, 60, 10)
        };
        var command = new BulkImportGamesCommand(games, Guid.NewGuid());
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithNullGames_FailsValidation()
    {
        var command = new BulkImportGamesCommand(null!, Guid.NewGuid());
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Games);
    }

    [Fact]
    public void Validate_WithEmptyGames_FailsValidation()
    {
        var command = new BulkImportGamesCommand(new List<BulkGameImportDto>(), Guid.NewGuid());
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Games);
    }

    [Fact]
    public void Validate_WithTooManyGames_FailsValidation()
    {
        var games = Enumerable.Range(1, 101)
            .Select(i => new BulkGameImportDto(i, null, null, null, null, null, null, null))
            .ToList();
        var command = new BulkImportGamesCommand(games, Guid.NewGuid());
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Games);
    }

    [Fact]
    public void Validate_WithGameWithoutBggIdOrTitle_FailsValidation()
    {
        var games = new List<BulkGameImportDto>
        {
            new BulkGameImportDto(null, null, null, null, null, null, null, null)
        };
        var command = new BulkImportGamesCommand(games, Guid.NewGuid());
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor("Games[0]");
    }

    [Fact]
    public void Validate_WithGameWithWhitespaceTitle_FailsValidation()
    {
        var games = new List<BulkGameImportDto>
        {
            new BulkGameImportDto(null, "   ", null, null, null, null, null, null)
        };
        var command = new BulkImportGamesCommand(games, Guid.NewGuid());
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor("Games[0]");
    }

    [Fact]
    public void Validate_WithEmptyUserId_FailsValidation()
    {
        var games = new List<BulkGameImportDto>
        {
            new BulkGameImportDto(123, null, null, null, null, null, null, null)
        };
        var command = new BulkImportGamesCommand(games, Guid.Empty);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.UserId);
    }
}