using Api.Infrastructure.Entities;
using Api.Services;
using Api.Tests.Constants;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Unit tests for LlmRequestLogEntity.
/// Issue #5076: Phase 1 test suite — entity field defaults and expiration logic.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "5076")]
public sealed class LlmRequestLogEntityTests
{
    [Fact]
    public void NewEntity_HasDefaultValues()
    {
        // Act
        var entity = new LlmRequestLogEntity
        {
            ModelId = "openai/gpt-4o",
            Provider = "openrouter"
        };

        // Assert
        entity.Id.Should().NotBe(Guid.Empty);
        (entity.RequestedAt <= DateTime.UtcNow).Should().BeTrue();
        (entity.RequestedAt > DateTime.UtcNow.AddSeconds(-5)).Should().BeTrue();
        entity.RequestSource.Should().Be("Manual"); // Default from enum name
        entity.UserId.Should().BeNull();
        entity.UserRole.Should().BeNull();
        entity.PromptTokens.Should().Be(0);
        entity.CompletionTokens.Should().Be(0);
        entity.TotalTokens.Should().Be(0);
        entity.CostUsd.Should().Be(0m);
        entity.LatencyMs.Should().Be(0);
        entity.Success.Should().BeFalse();
        entity.ErrorMessage.Should().BeNull();
        entity.IsStreaming.Should().BeFalse();
        entity.IsFreeModel.Should().BeFalse();
        entity.SessionId.Should().BeNull();
    }

    [Fact]
    public void NewEntity_UserRegion_DefaultsToNull()
    {
        // Act
        var entity = new LlmRequestLogEntity
        {
            ModelId = "openai/gpt-4o",
            Provider = "openrouter"
        };

        // Assert — Issue #27: UserRegion defaults to null
        entity.UserRegion.Should().BeNull();
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("it-IT")]
    [InlineData("de-DE")]
    [InlineData(null)]
    public void UserRegion_StoresAssignedValue(string? region)
    {
        // Arrange & Act
        var entity = new LlmRequestLogEntity
        {
            ModelId = "model",
            Provider = "provider",
            UserRegion = region
        };

        // Assert — Issue #27: UserRegion stores the assigned value
        entity.UserRegion.Should().Be(region);
    }

    [Fact]
    public void UniqueIds_EveryNewInstance()
    {
        // Act
        var a = new LlmRequestLogEntity { ModelId = "m", Provider = "p" };
        var b = new LlmRequestLogEntity { ModelId = "m", Provider = "p" };

        // Assert
        b.Id.Should().NotBe(a.Id);
    }

    [Fact]
    public void ExpiresAt_PropertyStoresAssignedValue()
    {
        // Arrange
        var entity = new LlmRequestLogEntity { ModelId = "m", Provider = "p" };
        var future = entity.RequestedAt.AddDays(30);

        // Act — repository sets this at save time; verify the property stores it faithfully
        entity.ExpiresAt = future;

        // Assert
        entity.ExpiresAt.Should().Be(future);
    }

    [Theory]
    [InlineData("Manual")]
    [InlineData("RagPipeline")]
    [InlineData("EventDriven")]
    [InlineData("AutomatedTest")]
    [InlineData("AgentTask")]
    [InlineData("AdminOperation")]
    public void RequestSource_AcceptsAllEnumStringValues(string sourceName)
    {
        // Arrange
        var entity = new LlmRequestLogEntity
        {
            ModelId = "model",
            Provider = "provider",
            RequestSource = sourceName
        };

        // Assert
        entity.RequestSource.Should().Be(sourceName);
    }

    [Theory]
    [InlineData("openai/gpt-4o", "openrouter", true, 500, 150, 650, 0.0025, 350)]
    [InlineData("llama3", "ollama", false, 100, 50, 150, 0.0, 120)]
    public void Entity_StoresAllFields(
        string modelId, string provider, bool success,
        int promptTokens, int completionTokens, int totalTokens,
        double costUsdDouble, int latencyMs)
    {
        // Arrange & Act
        var costUsd = (decimal)costUsdDouble;
        var entity = new LlmRequestLogEntity
        {
            ModelId = modelId,
            Provider = provider,
            Success = success,
            PromptTokens = promptTokens,
            CompletionTokens = completionTokens,
            TotalTokens = totalTokens,
            CostUsd = costUsd,
            LatencyMs = latencyMs,
            IsStreaming = true,
            IsFreeModel = costUsd == 0m,
            UserId = Guid.NewGuid(),
            UserRole = "User",
            SessionId = "sess-abc"
        };

        // Assert
        entity.ModelId.Should().Be(modelId);
        entity.Provider.Should().Be(provider);
        entity.Success.Should().Be(success);
        entity.PromptTokens.Should().Be(promptTokens);
        entity.CompletionTokens.Should().Be(completionTokens);
        entity.TotalTokens.Should().Be(totalTokens);
        entity.CostUsd.Should().Be(costUsd);
        entity.LatencyMs.Should().Be(latencyMs);
        entity.IsStreaming.Should().BeTrue();
        entity.UserId.Should().NotBeNull();
        entity.UserRole.Should().Be("User");
        entity.SessionId.Should().Be("sess-abc");
    }
}
