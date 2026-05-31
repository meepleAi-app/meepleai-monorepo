using System.Text.Json;

using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Application.Services;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Unit coverage for #1728 drill query handler.
///
/// The handler reads a single <see cref="AiRequestLogEntity"/> by id and
/// deserializes the optional jsonb columns (<c>ChunksJson</c> +
/// <c>BreakdownJson</c>) into the FE-facing DTOs. Corrupted JSON must
/// degrade gracefully (warn + null) — never throw — because the drill
/// endpoint is read-only telemetry.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GetAiQueryDrillQueryHandlerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static MeepleAiDbContext CreateInMemoryDb(string testName)
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"GetAiQueryDrillTests_{testName}_{Guid.NewGuid()}")
            .Options;
        return new MeepleAiDbContext(
            options,
            Mock.Of<IMediator>(),
            Mock.Of<IDomainEventCollector>());
    }

    private static GetAiQueryDrillQueryHandler CreateHandler(MeepleAiDbContext db)
    {
        return new GetAiQueryDrillQueryHandler(db, Mock.Of<ILogger<GetAiQueryDrillQueryHandler>>());
    }

    [Fact]
    public async Task Handle_WithEmptyGuid_ReturnsNullWithoutHittingTheDatabase()
    {
        await using var db = CreateInMemoryDb(nameof(Handle_WithEmptyGuid_ReturnsNullWithoutHittingTheDatabase));
        var handler = CreateHandler(db);

        var result = await handler.Handle(new GetAiQueryDrillQuery(Guid.Empty), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenIdNotFound_ReturnsNull()
    {
        await using var db = CreateInMemoryDb(nameof(Handle_WhenIdNotFound_ReturnsNull));
        var handler = CreateHandler(db);

        var result = await handler.Handle(new GetAiQueryDrillQuery(Guid.NewGuid()), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WithNullRequest_ThrowsArgumentNullException()
    {
        await using var db = CreateInMemoryDb(nameof(Handle_WithNullRequest_ThrowsArgumentNullException));
        var handler = CreateHandler(db);

        await FluentActions
            .Invoking(() => handler.Handle(null!, CancellationToken.None))
            .Should()
            .ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Handle_WhenEntityHasNoDrillPayload_ReturnsEmptyChunksAndNullBreakdown()
    {
        await using var db = CreateInMemoryDb(nameof(Handle_WhenEntityHasNoDrillPayload_ReturnsEmptyChunksAndNullBreakdown));
        var entity = SeedLog(db, ChunksJson: null, BreakdownJson: null);
        var handler = CreateHandler(db);

        var result = await handler.Handle(new GetAiQueryDrillQuery(entity.Id), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Request.Id.Should().Be(entity.Id);
        result.Chunks.Should().BeEmpty();
        result.Breakdown.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenEntityHasChunksJson_DeserializesChunks()
    {
        await using var db = CreateInMemoryDb(nameof(Handle_WhenEntityHasChunksJson_DeserializesChunks));
        var chunks = new[]
        {
            new RetrievedChunkDto
            {
                Id = "chunk-1",
                Score = 0.91,
                Text = "Players draw cards at end of turn.",
                Page = 12,
                ChunkIndex = 4,
                PdfName = "rulebook.pdf",
                Used = true,
            },
            new RetrievedChunkDto
            {
                Id = "chunk-2",
                Score = 0.74,
                Text = "Hand limit is 7.",
                Page = 14,
                ChunkIndex = 7,
                PdfName = "rulebook.pdf",
                Used = false,
            },
        };
        var entity = SeedLog(db, ChunksJson: JsonSerializer.Serialize(chunks, JsonOptions), BreakdownJson: null);
        var handler = CreateHandler(db);

        var result = await handler.Handle(new GetAiQueryDrillQuery(entity.Id), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Chunks.Should().HaveCount(2);
        result.Chunks[0].Id.Should().Be("chunk-1");
        result.Chunks[0].Score.Should().Be(0.91);
        result.Chunks[1].Used.Should().BeFalse();
        result.Breakdown.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenEntityHasBreakdownJson_DeserializesBreakdown()
    {
        await using var db = CreateInMemoryDb(nameof(Handle_WhenEntityHasBreakdownJson_DeserializesBreakdown));
        var breakdown = new LatencyBreakdownDto
        {
            RetrievalMs = 100,
            RerankMs = 50,
            LlmMs = 600,
            PostMs = 92,
        };
        var entity = SeedLog(db, ChunksJson: null, BreakdownJson: JsonSerializer.Serialize(breakdown, JsonOptions));
        var handler = CreateHandler(db);

        var result = await handler.Handle(new GetAiQueryDrillQuery(entity.Id), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Breakdown.Should().NotBeNull();
        result.Breakdown!.RetrievalMs.Should().Be(100);
        result.Breakdown.LlmMs.Should().Be(600);
        result.Chunks.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenChunksJsonIsCorrupted_ReturnsEmptyChunksWithoutThrowing()
    {
        await using var db = CreateInMemoryDb(nameof(Handle_WhenChunksJsonIsCorrupted_ReturnsEmptyChunksWithoutThrowing));
        var entity = SeedLog(db, ChunksJson: "{ this is not valid json", BreakdownJson: null);
        var handler = CreateHandler(db);

        var result = await handler.Handle(new GetAiQueryDrillQuery(entity.Id), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Chunks.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenBreakdownJsonIsCorrupted_ReturnsNullBreakdownWithoutThrowing()
    {
        await using var db = CreateInMemoryDb(nameof(Handle_WhenBreakdownJsonIsCorrupted_ReturnsNullBreakdownWithoutThrowing));
        var entity = SeedLog(db, ChunksJson: null, BreakdownJson: "{\"retrievalMs\": \"oops-not-a-number\"");
        var handler = CreateHandler(db);

        var result = await handler.Handle(new GetAiQueryDrillQuery(entity.Id), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Breakdown.Should().BeNull();
    }

    private static AiRequestLogEntity SeedLog(MeepleAiDbContext db, string? ChunksJson, string? BreakdownJson)
    {
        var entity = new AiRequestLogEntity
        {
            Id = Guid.NewGuid(),
            Endpoint = "qa-stream",
            Query = "test query",
            ResponseSnippet = "test response",
            LatencyMs = 842,
            TokenCount = 1240,
            PromptTokens = 980,
            CompletionTokens = 260,
            Status = "Success",
            CreatedAt = DateTime.UtcNow,
            ChunksJson = ChunksJson,
            BreakdownJson = BreakdownJson,
        };
        db.AiRequestLogs.Add(entity);
        db.SaveChanges();
        return entity;
    }
}
