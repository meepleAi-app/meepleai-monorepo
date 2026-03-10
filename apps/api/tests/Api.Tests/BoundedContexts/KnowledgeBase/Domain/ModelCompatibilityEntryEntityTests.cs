using Api.Infrastructure.Entities.KnowledgeBase;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain;

/// <summary>
/// Unit tests for ModelCompatibilityEntryEntity and ModelChangeLogEntity.
/// Issue #5496: Part of Epic #5490 - Model Versioning &amp; Availability Monitoring.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class ModelCompatibilityEntryEntityTests
{
    [Fact]
    public void ModelCompatibilityEntryEntity_DefaultValues_AreCorrect()
    {
        // Act
        var entity = new ModelCompatibilityEntryEntity();

        // Assert
        entity.Id.Should().NotBeEmpty();
        entity.Alternatives.Should().BeEmpty();
        entity.Strengths.Should().BeEmpty();
        entity.IsCurrentlyAvailable.Should().BeTrue();
        entity.IsDeprecated.Should().BeFalse();
        entity.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        entity.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        entity.LastVerifiedAt.Should().BeNull();
    }

    [Fact]
    public void ModelCompatibilityEntryEntity_CanSetAllProperties()
    {
        // Arrange & Act
        var entity = new ModelCompatibilityEntryEntity
        {
            ModelId = "meta-llama/llama-3.3-70b-instruct:free",
            DisplayName = "Llama 3.3 70B",
            Provider = "openrouter",
            Alternatives = new[] { "qwen/qwen-2.5-72b:free", "google/gemma-2-9b-it:free" },
            ContextWindow = 128000,
            Strengths = new[] { "reasoning", "multilingual", "speed" },
            IsCurrentlyAvailable = true,
            IsDeprecated = false,
            LastVerifiedAt = DateTime.UtcNow,
        };

        // Assert
        entity.ModelId.Should().Be("meta-llama/llama-3.3-70b-instruct:free");
        entity.DisplayName.Should().Be("Llama 3.3 70B");
        entity.Provider.Should().Be("openrouter");
        entity.Alternatives.Should().HaveCount(2);
        entity.ContextWindow.Should().Be(128000);
        entity.Strengths.Should().HaveCount(3);
    }

    [Fact]
    public void ModelChangeLogEntity_DefaultValues_AreCorrect()
    {
        // Act
        var entity = new ModelChangeLogEntity();

        // Assert
        entity.Id.Should().NotBeEmpty();
        entity.IsAutomatic.Should().BeFalse();
        entity.OccurredAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        entity.PreviousModelId.Should().BeNull();
        entity.NewModelId.Should().BeNull();
        entity.AffectedStrategy.Should().BeNull();
        entity.ChangedByUserId.Should().BeNull();
    }

    [Fact]
    public void ModelChangeLogEntity_CanSetAllProperties()
    {
        // Arrange & Act
        var userId = Guid.NewGuid();
        var entity = new ModelChangeLogEntity
        {
            ModelId = "meta-llama/llama-3.3-70b-instruct:free",
            ChangeType = "swapped",
            PreviousModelId = "meta-llama/llama-3.3-70b-instruct:free",
            NewModelId = "qwen/qwen-2.5-72b:free",
            AffectedStrategy = "Balanced",
            Reason = "Primary model deprecated, auto-fallback activated",
            IsAutomatic = true,
            ChangedByUserId = userId,
        };

        // Assert
        entity.ModelId.Should().Be("meta-llama/llama-3.3-70b-instruct:free");
        entity.ChangeType.Should().Be("swapped");
        entity.PreviousModelId.Should().Be("meta-llama/llama-3.3-70b-instruct:free");
        entity.NewModelId.Should().Be("qwen/qwen-2.5-72b:free");
        entity.AffectedStrategy.Should().Be("Balanced");
        entity.IsAutomatic.Should().BeTrue();
        entity.ChangedByUserId.Should().Be(userId);
    }

    [Fact]
    public void ModelChangeLogEntity_ChangeTypes_AreValid()
    {
        // Valid change types for the system
        var validTypes = new[] { "deprecated", "unavailable", "restored", "swapped", "fallback_activated" };

        foreach (var type in validTypes)
        {
            var entity = new ModelChangeLogEntity { ChangeType = type, ModelId = "test", Reason = "test" };
            entity.ChangeType.Should().Be(type);
        }
    }
}
