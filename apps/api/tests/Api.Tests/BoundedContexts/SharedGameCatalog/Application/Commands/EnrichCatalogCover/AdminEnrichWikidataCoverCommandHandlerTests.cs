using Api.BoundedContexts.SharedGameCatalog.Application.Commands.EnrichCatalogCover;
using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Commands.EnrichCatalogCover;

/// <summary>
/// Unit tests for <see cref="AdminEnrichWikidataCoverCommandHandler"/>.
/// Issue #1823 Wave 3 M12.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "1823")]
public class AdminEnrichWikidataCoverCommandHandlerTests
{
    private readonly Mock<IWikidataCoverEnrichmentRunner> _runner = new();

    private AdminEnrichWikidataCoverCommandHandler Sut() =>
        new(_runner.Object, NullLogger<AdminEnrichWikidataCoverCommandHandler>.Instance);

    [Fact]
    public async Task Handle_SuccessOutcome_FlattensToSuccessDto()
    {
        var gameId = Guid.NewGuid();
        _runner.Setup(r => r.EnrichAndRecordAsync(gameId, true, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnrichCatalogCoverResult.Success("covers/abc/cover", "CC BY-SA 4.0", "John Doe", "https://wikidata.org/wiki/Q1"));

        var result = await Sut().Handle(
            new AdminEnrichWikidataCoverCommand(gameId, ForceRefresh: true, TriggeredByUserId: Guid.NewGuid()),
            default);

        result.Outcome.Should().Be(AdminEnrichWikidataCoverResult.OutcomeSuccess);
        result.R2Key.Should().Be("covers/abc/cover");
        result.License.Should().Be("CC BY-SA 4.0");
        result.Attribution.Should().Be("John Doe");
        result.SourceUrl.Should().Be("https://wikidata.org/wiki/Q1");
        result.Reason.Should().BeNull();
        result.Details.Should().BeNull();
    }

    [Fact]
    public async Task Handle_SkippedOutcome_FlattensToSkippedDto()
    {
        var gameId = Guid.NewGuid();
        _runner.Setup(r => r.EnrichAndRecordAsync(gameId, false, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnrichCatalogCoverResult.Skipped(EnrichCatalogCoverCommandHandler.SkipReasonQidMissing));

        var result = await Sut().Handle(
            new AdminEnrichWikidataCoverCommand(gameId, ForceRefresh: false, TriggeredByUserId: Guid.NewGuid()),
            default);

        result.Outcome.Should().Be(AdminEnrichWikidataCoverResult.OutcomeSkipped);
        result.Reason.Should().Be(EnrichCatalogCoverCommandHandler.SkipReasonQidMissing);
        result.R2Key.Should().BeNull();
    }

    [Fact]
    public async Task Handle_FailedOutcome_FlattensToFailedDtoWithDetails()
    {
        var gameId = Guid.NewGuid();
        _runner.Setup(r => r.EnrichAndRecordAsync(gameId, false, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnrichCatalogCoverResult.Failed(
                EnrichCatalogCoverCommandHandler.FailReasonR2Upload, "503 service unavailable"));

        var result = await Sut().Handle(
            new AdminEnrichWikidataCoverCommand(gameId, ForceRefresh: false, TriggeredByUserId: Guid.NewGuid()),
            default);

        result.Outcome.Should().Be(AdminEnrichWikidataCoverResult.OutcomeFailed);
        result.Reason.Should().Be(EnrichCatalogCoverCommandHandler.FailReasonR2Upload);
        result.Details.Should().Be("503 service unavailable");
        result.R2Key.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ForwardsForceRefreshAndUserToRunner()
    {
        // F6 #1823 Phase F: command must forward both forceRefresh and the
        // admin user id (Guid? in the runner signature, non-nullable Guid on
        // the command) — the runner stamps it onto the persisted attempt row.
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _runner.Setup(r => r.EnrichAndRecordAsync(
                It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnrichCatalogCoverResult.Success("k", "CC0", null, "u"));

        await Sut().Handle(
            new AdminEnrichWikidataCoverCommand(gameId, ForceRefresh: true, TriggeredByUserId: userId),
            default);

        _runner.Verify(r => r.EnrichAndRecordAsync(gameId, true, (Guid?)userId, It.IsAny<CancellationToken>()),
            Times.Once,
            "command MUST forward forceRefresh AND TriggeredByUserId so the M8 freshness window is bypassed AND the admin trigger is persisted on the F6 attempt row");
    }
}
