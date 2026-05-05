using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure;

/// <summary>
/// Integration tests for the lifecycle endpoints of the M1.2 Mechanic Extractor
/// (ISSUE-524 follow-up / ADR-051 T5). Covers:
/// <list type="bullet">
///   <item><c>POST /admin/mechanic-analyses/{id}/submit-review</c> — Draft/Rejected → InReview.</item>
///   <item><c>POST /admin/mechanic-analyses/{id}/approve</c> — InReview → Published.</item>
///   <item><c>POST /admin/mechanic-analyses/{id}/suppress</c> — orthogonal T5 kill-switch.</item>
/// </list>
/// Analyses are seeded directly in the target lifecycle state (bypassing the generation pipeline)
/// to keep the tests focused on the command handlers' synchronous contract: state transitions,
/// 409 on invalid invariants, 404/422 mappings, and 401 for missing sessions.
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class MechanicAnalysisLifecycleEndpointsIntegrationTests : IAsyncLifetime
{
    private const string EndpointBase = "/api/v1/admin/mechanic-analyses";

    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private string _adminSessionToken = null!;
    private Mock<IBackgroundTaskService> _backgroundTaskMock = null!;

    internal static readonly Guid TestAdminId = Guid.NewGuid();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public MechanicAnalysisLifecycleEndpointsIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"mechanic_analysis_lifecycle_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);

        _backgroundTaskMock = new Mock<IBackgroundTaskService>();
        _backgroundTaskMock
            .Setup(b => b.ExecuteWithCancellation(It.IsAny<string>(), It.IsAny<Func<CancellationToken, Task>>()));

        _factory = IntegrationWebApplicationFactory
            .Create(connectionString)
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IBackgroundTaskService>();
                    services.AddSingleton<IBackgroundTaskService>(_backgroundTaskMock.Object);
                });
            });

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            await dbContext.Database.MigrateAsync();

            var (_, token) = await TestSessionHelper.CreateAdminSessionAsync(dbContext, TestAdminId);
            _adminSessionToken = token;
        }

        _client = _factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        _factory?.Dispose();
        await _fixture.DropIsolatedDatabaseAsync(_testDbName);
    }

    // ────────────────────────────────────────────────────────────────────────
    // submit-review
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SubmitForReview_FromDraftWithClaims_TransitionsToInReview()
    {
        // Arrange
        var sharedGameId = await SeedSharedGameAsync();
        var analysisId = await SeedAnalysisAsync(
            sharedGameId,
            status: 0, // Draft
            claimStatuses: new[] { 0, 0 }); // two Pending claims

        // Act
        var response = await SendLifecycleAsync("submit-review", analysisId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<MechanicAnalysisLifecycleResponseDto>(JsonOptions);
        dto.Should().NotBeNull();
        dto!.Id.Should().Be(analysisId);
        dto.Status.Should().Be(MechanicAnalysisStatus.InReview);
        dto.IsSuppressed.Should().BeFalse();
    }

    [Fact]
    public async Task SubmitForReview_FromRejected_TransitionsToInReviewAndResetsPendingClaims()
    {
        // Arrange
        var sharedGameId = await SeedSharedGameAsync();
        var analysisId = await SeedAnalysisAsync(
            sharedGameId,
            status: 3, // Rejected
            claimStatuses: new[] { 1, 2 }, // Approved + Rejected (domain should reset Rejected to Pending on resubmit)
            rejectionReason: "Needs more work");

        // Act
        var response = await SendLifecycleAsync("submit-review", analysisId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<MechanicAnalysisLifecycleResponseDto>(JsonOptions);
        dto!.Status.Should().Be(MechanicAnalysisStatus.InReview);
    }

    [Fact]
    public async Task SubmitForReview_FromInReview_Returns409()
    {
        // Arrange
        var sharedGameId = await SeedSharedGameAsync();
        var analysisId = await SeedAnalysisAsync(sharedGameId, status: 1, claimStatuses: new[] { 0 });

        // Act
        var response = await SendLifecycleAsync("submit-review", analysisId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task SubmitForReview_FromPublished_Returns409()
    {
        // Arrange
        var sharedGameId = await SeedSharedGameAsync();
        var analysisId = await SeedAnalysisAsync(sharedGameId, status: 2, claimStatuses: new[] { 1 });

        // Act
        var response = await SendLifecycleAsync("submit-review", analysisId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task SubmitForReview_WithNoClaims_Returns409()
    {
        // Arrange
        var sharedGameId = await SeedSharedGameAsync();
        var analysisId = await SeedAnalysisAsync(sharedGameId, status: 0, claimStatuses: Array.Empty<int>());

        // Act
        var response = await SendLifecycleAsync("submit-review", analysisId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task SubmitForReview_UnknownAnalysisId_Returns404()
    {
        // Act
        var response = await SendLifecycleAsync("submit-review", Guid.NewGuid());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SubmitForReview_WithoutSession_Returns401()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Post, $"{EndpointBase}/{Guid.NewGuid()}/submit-review");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ────────────────────────────────────────────────────────────────────────
    // approve
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Approve_FromInReviewWithAllClaimsApproved_TransitionsToPublished()
    {
        // Arrange
        var sharedGameId = await SeedSharedGameAsync();
        var analysisId = await SeedAnalysisAsync(
            sharedGameId,
            status: 1, // InReview
            claimStatuses: new[] { 1, 1, 1 }); // all Approved

        // Act
        var response = await SendLifecycleAsync("approve", analysisId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<MechanicAnalysisLifecycleResponseDto>(JsonOptions);
        dto!.Status.Should().Be(MechanicAnalysisStatus.Published);
        dto.ReviewedBy.Should().Be(TestAdminId);
        dto.ReviewedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Approve_WithPendingClaim_Returns409()
    {
        // Arrange
        var sharedGameId = await SeedSharedGameAsync();
        var analysisId = await SeedAnalysisAsync(
            sharedGameId,
            status: 1,
            claimStatuses: new[] { 1, 0 }); // one Approved, one Pending

        // Act
        var response = await SendLifecycleAsync("approve", analysisId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Approve_WithRejectedClaim_Returns409()
    {
        // Arrange
        var sharedGameId = await SeedSharedGameAsync();
        var analysisId = await SeedAnalysisAsync(
            sharedGameId,
            status: 1,
            claimStatuses: new[] { 1, 2 }); // one Approved, one Rejected

        // Act
        var response = await SendLifecycleAsync("approve", analysisId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Approve_FromDraft_Returns409()
    {
        // Arrange
        var sharedGameId = await SeedSharedGameAsync();
        var analysisId = await SeedAnalysisAsync(sharedGameId, status: 0, claimStatuses: new[] { 1 });

        // Act
        var response = await SendLifecycleAsync("approve", analysisId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Approve_UnknownAnalysisId_Returns404()
    {
        // Act
        var response = await SendLifecycleAsync("approve", Guid.NewGuid());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Approve_WithoutSession_Returns401()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Post, $"{EndpointBase}/{Guid.NewGuid()}/approve");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ────────────────────────────────────────────────────────────────────────
    // suppress — T5 kill-switch (orthogonal to lifecycle)
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Suppress_FromPublished_AppliesKillSwitchAndPreservesStatus()
    {
        // Arrange — orthogonal: Published remains Published, just flagged as suppressed.
        var sharedGameId = await SeedSharedGameAsync();
        var analysisId = await SeedAnalysisAsync(sharedGameId, status: 2, claimStatuses: new[] { 1 });

        var body = new SuppressBody(
            Reason: "DMCA takedown received from publisher on behalf of Acme Games Ltd.",
            RequestSource: SuppressionRequestSource.Legal,
            RequestedAt: DateTime.SpecifyKind(DateTime.UtcNow.AddHours(-2), DateTimeKind.Utc));

        // Act
        var response = await SendSuppressAsync(analysisId, body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<MechanicAnalysisLifecycleResponseDto>(JsonOptions);
        dto!.Status.Should().Be(MechanicAnalysisStatus.Published);
        dto.IsSuppressed.Should().BeTrue();
        dto.SuppressedBy.Should().Be(TestAdminId);
        dto.SuppressedAt.Should().NotBeNull();
        dto.SuppressionReason.Should().Be(body.Reason);
        dto.SuppressionRequestSource.Should().Be(SuppressionRequestSource.Legal);
    }

    [Fact]
    public async Task Suppress_FromDraft_AppliesKillSwitch()
    {
        // Arrange
        var sharedGameId = await SeedSharedGameAsync();
        var analysisId = await SeedAnalysisAsync(sharedGameId, status: 0, claimStatuses: new[] { 0 });

        var body = new SuppressBody(
            Reason: "Operator initiated internal suppression for policy violation review.",
            RequestSource: SuppressionRequestSource.Other,
            RequestedAt: null);

        // Act
        var response = await SendSuppressAsync(analysisId, body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<MechanicAnalysisLifecycleResponseDto>(JsonOptions);
        dto!.IsSuppressed.Should().BeTrue();
        dto.Status.Should().Be(MechanicAnalysisStatus.Draft);
    }

    [Fact]
    public async Task Suppress_AlreadySuppressed_Returns409()
    {
        // Arrange
        var sharedGameId = await SeedSharedGameAsync();
        var analysisId = await SeedAnalysisAsync(
            sharedGameId,
            status: 2,
            claimStatuses: new[] { 1 },
            isSuppressed: true);

        var body = new SuppressBody(
            Reason: "Second takedown request from a different complainant.",
            RequestSource: SuppressionRequestSource.Email,
            RequestedAt: null);

        // Act
        var response = await SendSuppressAsync(analysisId, body);

        // Assert — aggregate throws InvalidOperationException → ConflictException 409.
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Suppress_ReasonTooShort_Returns422()
    {
        // Arrange
        var sharedGameId = await SeedSharedGameAsync();
        var analysisId = await SeedAnalysisAsync(sharedGameId, status: 2, claimStatuses: new[] { 1 });

        var body = new SuppressBody(
            Reason: "too short", // 9 chars, below the 20-char minimum
            RequestSource: SuppressionRequestSource.Email,
            RequestedAt: null);

        // Act
        var response = await SendSuppressAsync(analysisId, body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Suppress_ReasonTooLong_Returns422()
    {
        // Arrange
        var sharedGameId = await SeedSharedGameAsync();
        var analysisId = await SeedAnalysisAsync(sharedGameId, status: 2, claimStatuses: new[] { 1 });

        var body = new SuppressBody(
            Reason: new string('x', 501), // 501 chars, above the 500-char maximum
            RequestSource: SuppressionRequestSource.Email,
            RequestedAt: null);

        // Act
        var response = await SendSuppressAsync(analysisId, body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Suppress_InvalidEnumValue_Returns422()
    {
        // Arrange
        var sharedGameId = await SeedSharedGameAsync();
        var analysisId = await SeedAnalysisAsync(sharedGameId, status: 2, claimStatuses: new[] { 1 });

        // Send the request source as an out-of-range integer. System.Text.Json binds it to the
        // enum without range-checking, so the FluentValidation IsInEnum rule fires → 422.
        var payload = new
        {
            reason = "Valid reason that exceeds the 20 character minimum length requirement.",
            requestSource = 999,
            requestedAt = (DateTime?)null
        };

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"{EndpointBase}/{analysisId}/suppress",
            _adminSessionToken,
            payload);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Suppress_UnknownAnalysisId_Returns404()
    {
        // Arrange
        var body = new SuppressBody(
            Reason: "Legitimate reason string exceeding the twenty character minimum.",
            RequestSource: SuppressionRequestSource.Email,
            RequestedAt: null);

        // Act
        var response = await SendSuppressAsync(Guid.NewGuid(), body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Suppress_WithoutSession_Returns401()
    {
        // Arrange
        var body = new SuppressBody(
            Reason: "Legitimate reason string exceeding the twenty character minimum.",
            RequestSource: SuppressionRequestSource.Email,
            RequestedAt: null);

        var request = new HttpRequestMessage(HttpMethod.Post, $"{EndpointBase}/{Guid.NewGuid()}/suppress")
        {
            Content = JsonContent.Create(body)
        };

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Helpers
    // ────────────────────────────────────────────────────────────────────────

    private async Task<HttpResponseMessage> SendLifecycleAsync(string segment, Guid analysisId)
    {
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"{EndpointBase}/{analysisId}/{segment}",
            _adminSessionToken);
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> SendSuppressAsync(Guid analysisId, SuppressBody body)
    {
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"{EndpointBase}/{analysisId}/suppress",
            _adminSessionToken,
            body);
        return await _client.SendAsync(request);
    }

    private async Task<Guid> SeedSharedGameAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var gameId = Guid.NewGuid();
        var game = new SharedGameEntity
        {
            Id = gameId,
            Title = $"Lifecycle Test Game {Guid.NewGuid():N}",
            Description = "Integration test rulebook",
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 90,
            YearPublished = 2024,
            MinAge = 12,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = TestAdminId
        };
        dbContext.Set<SharedGameEntity>().Add(game);
        await dbContext.SaveChangesAsync();
        return gameId;
    }

    /// <summary>
    /// Seeds a <see cref="MechanicAnalysisEntity"/> directly in the requested lifecycle status
    /// with the given claim statuses. Bypasses the generation pipeline; the analysis references
    /// a synthetic PdfDocumentId so no PDF or SharedGameDocument link needs to exist for the
    /// lifecycle commands (they only touch the analysis aggregate).
    /// </summary>
    private async Task<Guid> SeedAnalysisAsync(
        Guid sharedGameId,
        int status,
        int[] claimStatuses,
        bool isSuppressed = false,
        string? rejectionReason = null)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var analysisId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var analysis = new MechanicAnalysisEntity
        {
            Id = analysisId,
            SharedGameId = sharedGameId,
            PdfDocumentId = Guid.NewGuid(),
            PromptVersion = "mechanic-extractor-v1",
            Status = status,
            CreatedBy = TestAdminId,
            CreatedAt = now,
            ReviewedBy = status == 1 || status == 2 ? TestAdminId : null,
            ReviewedAt = status == 1 || status == 2 ? now : null,
            RejectionReason = rejectionReason,
            TotalTokensUsed = 1234,
            EstimatedCostUsd = 0.05m,
            ModelUsed = "test-model",
            Provider = "test-provider",
            CostCapUsd = 1.00m,
            IsSuppressed = isSuppressed,
            SuppressedAt = isSuppressed ? now : null,
            SuppressedBy = isSuppressed ? TestAdminId : null,
            SuppressionReason = isSuppressed ? "pre-existing suppression for integration test" : null,
            SuppressionRequestSource = isSuppressed ? (int)SuppressionRequestSource.Email : null
        };
        dbContext.Set<MechanicAnalysisEntity>().Add(analysis);

        for (int i = 0; i < claimStatuses.Length; i++)
        {
            var claim = new MechanicClaimEntity
            {
                Id = Guid.NewGuid(),
                AnalysisId = analysisId,
                Section = i % 6,
                Text = $"Claim {i}: synthetic mechanic description for lifecycle tests.",
                DisplayOrder = i,
                Status = claimStatuses[i],
                ReviewedBy = claimStatuses[i] == 0 ? null : TestAdminId,
                ReviewedAt = claimStatuses[i] == 0 ? null : now,
                RejectionNote = claimStatuses[i] == 2 ? "test rejection note" : null
            };
            dbContext.Set<MechanicClaimEntity>().Add(claim);
        }

        await dbContext.SaveChangesAsync();
        return analysisId;
    }

    // Mirrors AdminMechanicAnalysesEndpoints.SuppressMechanicAnalysisRequest (internal).
    private sealed record SuppressBody(
        string Reason,
        SuppressionRequestSource RequestSource,
        DateTime? RequestedAt);
}
