using Api.Tests.E2E.Infrastructure;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using Xunit;

#pragma warning disable S1144 // Unused private types or members should be removed (DTOs for deserialization)

namespace Api.Tests.E2E.KnowledgeBase;

/// <summary>
/// E2E tests for AI models endpoint.
/// Tests model listing with tier filtering.
///
/// Issue #3377: Models Tier Endpoint
///
/// Critical Journeys Covered:
/// - List all available models
/// - Filter models by tier
/// - Invalid tier handling
/// </summary>
[Collection("E2ETests")]
[Trait("Category", "E2E")]
public sealed class ModelsE2ETests : E2ETestBase
{
    public ModelsE2ETests(E2ETestFixture fixture) : base(fixture) { }

    protected override Task SeedTestDataAsync()
    {
        // No seed data required - models endpoint doesn't require authentication
        // and uses in-memory configuration
        return Task.CompletedTask;
    }

    #region Get All Models Tests

    [Fact]
    public async Task GetModels_NoFilter_ReturnsAllModels()
    {
        // Arrange & Act
        var response = await Client.GetAsync("/api/v1/models");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ModelsResponse>();
        result.Should().NotBeNull();
        result!.Models.Should().NotBeEmpty();

        // Verify model structure
        var firstModel = result.Models[0];
        firstModel.Id.Should().NotBeNullOrWhiteSpace();
        firstModel.Name.Should().NotBeNullOrWhiteSpace();
        firstModel.Provider.Should().NotBeNullOrWhiteSpace();
        firstModel.Tier.Should().NotBeNullOrWhiteSpace();
        firstModel.MaxTokens.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetModels_NoFilter_ContainsExpectedModels()
    {
        // Arrange & Act
        var response = await Client.GetAsync("/api/v1/models");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ModelsResponse>();
        result.Should().NotBeNull();

        // Should contain some known models
        var modelIds = result!.Models.Select(m => m.Id).ToList();
        modelIds.Should().Contain(id => id.Contains("llama", StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region Get Models By Tier Tests

    [Theory]
    [InlineData("free")]
    [InlineData("normal")]
    [InlineData("premium")]
    [InlineData("custom")]
    public async Task GetModels_WithValidTier_ReturnsFilteredModels(string tier)
    {
        // Arrange & Act
        var response = await Client.GetAsync($"/api/v1/models?tier={tier}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ModelsResponse>();
        result.Should().NotBeNull();
        result!.Models.Should().NotBeEmpty();

        // All returned models should be at or below the specified tier
        var validTiers = tier.ToLowerInvariant() switch
        {
            "free" => new[] { "free" },
            "normal" => new[] { "free", "normal" },
            "premium" => new[] { "free", "normal", "premium" },
            "custom" => new[] { "free", "normal", "premium", "custom" },
            _ => Array.Empty<string>()
        };

        result.Models.Should().AllSatisfy(m =>
            validTiers.Should().Contain(m.Tier));
    }

    [Fact]
    public async Task GetModels_FreeTier_ReturnsOnlyFreeModels()
    {
        // Arrange & Act
        var response = await Client.GetAsync("/api/v1/models?tier=free");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ModelsResponse>();
        result.Should().NotBeNull();
        result!.Models.Should().NotBeEmpty();

        // All models should be free tier
        result.Models.Should().AllSatisfy(m => m.Tier.Should().Be("free"));

        // Free models should have zero cost
        result.Models.Should().AllSatisfy(m =>
        {
            m.CostPer1kInputTokens.Should().Be(0);
            m.CostPer1kOutputTokens.Should().Be(0);
        });
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("enterprise")]
    [InlineData("basic")]
    public async Task GetModels_InvalidTier_ReturnsEmptyList(string invalidTier)
    {
        // Arrange & Act
        var response = await Client.GetAsync($"/api/v1/models?tier={invalidTier}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ModelsResponse>();
        result.Should().NotBeNull();
        result!.Models.Should().BeEmpty();
    }

    #endregion

    #region Model Tier Hierarchy Tests

    [Fact]
    public async Task GetModels_TierHierarchy_HigherTiersIncludeLowerTierModels()
    {
        // Get models for each tier
        var freeResponse = await Client.GetAsync("/api/v1/models?tier=free");
        var normalResponse = await Client.GetAsync("/api/v1/models?tier=normal");
        var premiumResponse = await Client.GetAsync("/api/v1/models?tier=premium");
        var customResponse = await Client.GetAsync("/api/v1/models?tier=custom");

        var freeModels = (await freeResponse.Content.ReadFromJsonAsync<ModelsResponse>())!.Models;
        var normalModels = (await normalResponse.Content.ReadFromJsonAsync<ModelsResponse>())!.Models;
        var premiumModels = (await premiumResponse.Content.ReadFromJsonAsync<ModelsResponse>())!.Models;
        var customModels = (await customResponse.Content.ReadFromJsonAsync<ModelsResponse>())!.Models;

        // Higher tiers should include all models from lower tiers
        freeModels.Count.Should().BeLessThanOrEqualTo(normalModels.Count);
        normalModels.Count.Should().BeLessThanOrEqualTo(premiumModels.Count);
        premiumModels.Count.Should().BeLessThanOrEqualTo(customModels.Count);

        // Free model IDs should all be present in normal tier
        var freeModelIds = freeModels.Select(m => m.Id).ToHashSet();
        var normalModelIds = normalModels.Select(m => m.Id).ToHashSet();
        freeModelIds.Should().BeSubsetOf(normalModelIds);

        // Normal model IDs should all be present in premium tier
        var premiumModelIds = premiumModels.Select(m => m.Id).ToHashSet();
        normalModelIds.Should().BeSubsetOf(premiumModelIds);
    }

    #endregion

    #region Response DTOs

    private sealed record ModelsResponse(IReadOnlyList<ModelDto> Models);

    private sealed record ModelDto(
        string Id,
        string Name,
        string Provider,
        string Tier,
        decimal CostPer1kInputTokens,
        decimal CostPer1kOutputTokens,
        int MaxTokens,
        bool SupportsStreaming,
        string? Description);

    #endregion
}
