using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.BoundedContexts.UserLibrary.Application.Validators;
using Api.BoundedContexts.UserLibrary.Domain.Enums;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Validators;

/// <summary>
/// Tests for validators across all entity types to ensure consistency.
/// Issue #4263: Phase 2 - Generic UserCollection System
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public class EntityValidatorTests
{
    [Theory]
    [InlineData(EntityType.Game)]
    [InlineData(EntityType.Player)]
    [InlineData(EntityType.Event)]
    [InlineData(EntityType.Session)]
    [InlineData(EntityType.Agent)]
    [InlineData(EntityType.Document)]
    [InlineData(EntityType.ChatSession)]
    public void AddToCollectionCommand_ValidatesAllEntityTypes(EntityType entityType)
    {
        // Arrange
        var validator = new AddToCollectionCommandValidator();
        var command = new AddToCollectionCommand(
            Guid.NewGuid(),
            entityType,
            Guid.NewGuid()
        );

        // Act
        var result = validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(EntityType.Game)]
    [InlineData(EntityType.Player)]
    [InlineData(EntityType.Event)]
    [InlineData(EntityType.Session)]
    [InlineData(EntityType.Agent)]
    [InlineData(EntityType.Document)]
    [InlineData(EntityType.ChatSession)]
    public void GetCollectionStatusQuery_ValidatesAllEntityTypes(EntityType entityType)
    {
        // Arrange
        var validator = new GetCollectionStatusQueryValidator();
        var query = new GetCollectionStatusQuery(
            Guid.NewGuid(),
            entityType,
            Guid.NewGuid()
        );

        // Act
        var result = validator.TestValidate(query);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(EntityType.Game)]
    [InlineData(EntityType.Player)]
    [InlineData(EntityType.Event)]
    [InlineData(EntityType.Session)]
    [InlineData(EntityType.Agent)]
    [InlineData(EntityType.Document)]
    [InlineData(EntityType.ChatSession)]
    public void RemoveFromCollectionCommand_ValidatesAllEntityTypes(EntityType entityType)
    {
        // Arrange
        var validator = new RemoveFromCollectionCommandValidator();
        var command = new RemoveFromCollectionCommand(
            Guid.NewGuid(),
            entityType,
            Guid.NewGuid()
        );

        // Act
        var result = validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
