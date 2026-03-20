using System.Text.Json;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.Entities;

/// <summary>
/// Tests for the GameStateTemplate entity.
/// Issue #3025: Backend 90% Coverage Target - Phase 12
/// </summary>
[Trait("Category", "Unit")]
public sealed class GameStateTemplateTests : IDisposable
{
    private readonly List<JsonDocument> _documents = new();

    private JsonDocument CreateSchema(string json = "{\"type\":\"object\"}")
    {
        var doc = JsonDocument.Parse(json);
        _documents.Add(doc);
        return doc;
    }

    public void Dispose()
    {
        foreach (var doc in _documents)
        {
            doc.Dispose();
        }
    }

    #region CreateFromAI Tests

    [Fact]
    public void CreateFromAI_WithValidData_ReturnsGameStateTemplate()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();
        var createdBy = Guid.NewGuid();
        var schema = CreateSchema();

        // Act
        var template = GameStateTemplate.CreateFromAI(
            sharedGameId,
            "Catan State Template",
            schema,
            createdBy,
            0.85m);

        // Assert
        template.Id.Should().NotBe(Guid.Empty);
        template.SharedGameId.Should().Be(sharedGameId);
        template.Name.Should().Be("Catan State Template");
        template.Schema.Should().NotBeNull();
        template.ConfidenceScore.Should().Be(0.85m);
        template.Source.Should().Be(GenerationSource.AI);
        template.IsActive.Should().BeFalse();
        template.CreatedBy.Should().Be(createdBy);
        template.GeneratedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void CreateFromAI_WithVersion_SetsVersion()
    {
        // Arrange
        var schema = CreateSchema();

        // Act
        var template = GameStateTemplate.CreateFromAI(
            Guid.NewGuid(), "Template", schema, Guid.NewGuid(), 0.9m, "2.0");

        // Assert
        template.Version.Should().Be("2.0");
    }

    [Fact]
    public void CreateFromAI_WithoutVersion_UsesDefaultVersion()
    {
        // Arrange
        var schema = CreateSchema();

        // Act
        var template = GameStateTemplate.CreateFromAI(
            Guid.NewGuid(), "Template", schema, Guid.NewGuid(), 0.9m);

        // Assert
        template.Version.Should().Be("1.0");
    }

    #endregion

    #region CreateManual Tests

    [Fact]
    public void CreateManual_WithValidData_ReturnsGameStateTemplate()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();
        var createdBy = Guid.NewGuid();
        var schema = CreateSchema();

        // Act
        var template = GameStateTemplate.CreateManual(
            sharedGameId,
            "Manual Template",
            schema,
            createdBy);

        // Assert
        template.Source.Should().Be(GenerationSource.Manual);
        template.ConfidenceScore.Should().BeNull();
        template.IsActive.Should().BeFalse();
    }

    [Fact]
    public void CreateManual_WithVersion_SetsVersion()
    {
        // Arrange
        var schema = CreateSchema();

        // Act
        var template = GameStateTemplate.CreateManual(
            Guid.NewGuid(), "Template", schema, Guid.NewGuid(), "1.5");

        // Assert
        template.Version.Should().Be("1.5");
    }

    #endregion

    #region Validation Tests

    [Fact]
    public void CreateFromAI_WithEmptySharedGameId_ThrowsArgumentException()
    {
        // Arrange
        var schema = CreateSchema();

        // Act
        var action = () => GameStateTemplate.CreateFromAI(
            Guid.Empty, "Template", schema, Guid.NewGuid(), 0.9m);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*SharedGameId cannot be empty*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateFromAI_WithEmptyName_ThrowsArgumentException(string? name)
    {
        // Arrange
        var schema = CreateSchema();

        // Act
        var action = () => GameStateTemplate.CreateFromAI(
            Guid.NewGuid(), name!, schema, Guid.NewGuid(), 0.9m);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Name cannot be empty*");
    }

    [Fact]
    public void CreateFromAI_WithNameExceeding200Characters_ThrowsArgumentException()
    {
        // Arrange
        var schema = CreateSchema();
        var longName = new string('A', 201);

        // Act
        var action = () => GameStateTemplate.CreateFromAI(
            Guid.NewGuid(), longName, schema, Guid.NewGuid(), 0.9m);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Name cannot exceed 200 characters*");
    }

    [Fact]
    public void CreateFromAI_WithNameAt200Characters_Succeeds()
    {
        // Arrange
        var schema = CreateSchema();
        var name = new string('A', 200);

        // Act
        var template = GameStateTemplate.CreateFromAI(
            Guid.NewGuid(), name, schema, Guid.NewGuid(), 0.9m);

        // Assert
        template.Name.Should().HaveLength(200);
    }

    [Fact]
    public void CreateFromAI_WithEmptyCreatedBy_ThrowsArgumentException()
    {
        // Arrange
        var schema = CreateSchema();

        // Act
        var action = () => GameStateTemplate.CreateFromAI(
            Guid.NewGuid(), "Template", schema, Guid.Empty, 0.9m);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*CreatedBy cannot be empty*");
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(-1)]
    [InlineData(2)]
    public void CreateFromAI_WithInvalidConfidenceScore_ThrowsArgumentOutOfRangeException(decimal score)
    {
        // Arrange
        var schema = CreateSchema();

        // Act
        var action = () => GameStateTemplate.CreateFromAI(
            Guid.NewGuid(), "Template", schema, Guid.NewGuid(), score);

        // Assert
        action.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*Confidence score must be between 0 and 1*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.5)]
    [InlineData(1)]
    public void CreateFromAI_WithValidConfidenceScore_Succeeds(decimal score)
    {
        // Arrange
        var schema = CreateSchema();

        // Act
        var template = GameStateTemplate.CreateFromAI(
            Guid.NewGuid(), "Template", schema, Guid.NewGuid(), score);

        // Assert
        template.ConfidenceScore.Should().Be(score);
    }

    #endregion

    #region SetAsActive Tests

    [Fact]
    public void SetAsActive_SetsIsActiveToTrue()
    {
        // Arrange
        var schema = CreateSchema();
        var template = GameStateTemplate.CreateFromAI(
            Guid.NewGuid(), "Template", schema, Guid.NewGuid(), 0.9m);
        template.IsActive.Should().BeFalse();

        // Act
        template.SetAsActive();

        // Assert
        template.IsActive.Should().BeTrue();
    }

    #endregion

    #region Deactivate Tests

    [Fact]
    public void Deactivate_SetsIsActiveToFalse()
    {
        // Arrange
        var schema = CreateSchema();
        var template = GameStateTemplate.CreateFromAI(
            Guid.NewGuid(), "Template", schema, Guid.NewGuid(), 0.9m);
        template.SetAsActive();
        template.IsActive.Should().BeTrue();

        // Act
        template.Deactivate();

        // Assert
        template.IsActive.Should().BeFalse();
    }

    #endregion

    #region UpdateName Tests

    [Fact]
    public void UpdateName_WithValidName_UpdatesName()
    {
        // Arrange
        var schema = CreateSchema();
        var template = GameStateTemplate.CreateFromAI(
            Guid.NewGuid(), "Original Name", schema, Guid.NewGuid(), 0.9m);

        // Act
        template.UpdateName("Updated Name");

        // Assert
        template.Name.Should().Be("Updated Name");
    }

    [Fact]
    public void UpdateName_TrimsName()
    {
        // Arrange
        var schema = CreateSchema();
        var template = GameStateTemplate.CreateFromAI(
            Guid.NewGuid(), "Original", schema, Guid.NewGuid(), 0.9m);

        // Act
        template.UpdateName("  Updated  ");

        // Assert
        template.Name.Should().Be("Updated");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateName_WithEmptyName_ThrowsArgumentException(string? name)
    {
        // Arrange
        var schema = CreateSchema();
        var template = GameStateTemplate.CreateFromAI(
            Guid.NewGuid(), "Original", schema, Guid.NewGuid(), 0.9m);

        // Act
        var action = () => template.UpdateName(name!);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Name cannot be empty*");
    }

    [Fact]
    public void UpdateName_WithNameExceeding200Characters_ThrowsArgumentException()
    {
        // Arrange
        var schema = CreateSchema();
        var template = GameStateTemplate.CreateFromAI(
            Guid.NewGuid(), "Original", schema, Guid.NewGuid(), 0.9m);
        var longName = new string('A', 201);

        // Act
        var action = () => template.UpdateName(longName);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Name cannot exceed 200 characters*");
    }

    #endregion

    #region UpdateSchema Tests

    [Fact]
    public void UpdateSchema_WithNewerVersion_UpdatesSchemaAndVersion()
    {
        // Arrange
        var schema1 = CreateSchema("{\"type\":\"object\",\"properties\":{}}");
        var schema2 = CreateSchema("{\"type\":\"object\",\"properties\":{\"score\":{}}}");
        var template = GameStateTemplate.CreateFromAI(
            Guid.NewGuid(), "Template", schema1, Guid.NewGuid(), 0.9m, "1.0");

        // Act
        template.UpdateSchema(schema2, "2.0");

        // Assert
        template.Version.Should().Be("2.0");
        template.Source.Should().Be(GenerationSource.Manual);
        template.ConfidenceScore.Should().BeNull();
    }

    [Fact]
    public void UpdateSchema_WithSameVersion_ThrowsInvalidOperationException()
    {
        // Arrange
        var schema1 = CreateSchema();
        var schema2 = CreateSchema("{\"type\":\"array\"}");
        var template = GameStateTemplate.CreateFromAI(
            Guid.NewGuid(), "Template", schema1, Guid.NewGuid(), 0.9m, "1.0");

        // Act
        var action = () => template.UpdateSchema(schema2, "1.0");

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*New version 1.0 must be greater than current version 1.0*");
    }

    [Fact]
    public void UpdateSchema_WithOlderVersion_ThrowsInvalidOperationException()
    {
        // Arrange
        var schema1 = CreateSchema();
        var schema2 = CreateSchema("{\"type\":\"array\"}");
        var template = GameStateTemplate.CreateFromAI(
            Guid.NewGuid(), "Template", schema1, Guid.NewGuid(), 0.9m, "2.0");

        // Act
        var action = () => template.UpdateSchema(schema2, "1.0");

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*New version 1.0 must be greater than current version 2.0*");
    }

    [Fact]
    public void UpdateSchema_WithNullSchema_ThrowsArgumentNullException()
    {
        // Arrange
        var schema = CreateSchema();
        var template = GameStateTemplate.CreateFromAI(
            Guid.NewGuid(), "Template", schema, Guid.NewGuid(), 0.9m);

        // Act
        var action = () => template.UpdateSchema(null!, "2.0");

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region GetSchemaAsString Tests

    [Fact]
    public void GetSchemaAsString_WithSchema_ReturnsJsonString()
    {
        // Arrange
        var schema = CreateSchema("{\"type\":\"object\",\"properties\":{}}");
        var template = GameStateTemplate.CreateFromAI(
            Guid.NewGuid(), "Template", schema, Guid.NewGuid(), 0.9m);

        // Act
        var result = template.GetSchemaAsString();

        // Assert
        result.Should().Contain("\"type\":\"object\"");
        result.Should().Contain("\"properties\"");
    }

    [Fact]
    public void GetSchemaAsString_WithNullSchema_ReturnsNull()
    {
        // Arrange - Use internal constructor to create template with null schema
        var template = new GameStateTemplate(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Template",
            null,
            "1.0",
            false,
            GenerationSource.Manual,
            null,
            DateTime.UtcNow,
            Guid.NewGuid());

        // Act
        var result = template.GetSchemaAsString();

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region Property Tests

    [Fact]
    public void Properties_ReturnCorrectValues()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();
        var createdBy = Guid.NewGuid();
        var schema = CreateSchema();

        // Act
        var template = GameStateTemplate.CreateFromAI(
            sharedGameId, "Test Template", schema, createdBy, 0.75m, "1.5");

        // Assert
        template.SharedGameId.Should().Be(sharedGameId);
        template.Name.Should().Be("Test Template");
        template.Version.Should().Be("1.5");
        template.Source.Should().Be(GenerationSource.AI);
        template.ConfidenceScore.Should().Be(0.75m);
        template.CreatedBy.Should().Be(createdBy);
        template.IsActive.Should().BeFalse();
    }

    #endregion
}
