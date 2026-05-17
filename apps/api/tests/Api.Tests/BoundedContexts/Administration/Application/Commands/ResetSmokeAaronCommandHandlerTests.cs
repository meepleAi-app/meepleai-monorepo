using Api.BoundedContexts.Administration.Application.Commands;
using Api.Infrastructure;
using Api.Infrastructure.Entities.UserLibrary;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Unit tests for <see cref="ResetSmokeAaronCommandHandler"/>. Issue #943 Phase A.
/// Covers the triple-gate security model + cascade-delete invariants.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
public sealed class ResetSmokeAaronCommandHandlerTests : IDisposable
{
    private readonly MeepleAiDbContext _dbContext;

    public ResetSmokeAaronCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"ResetSmokeAaron_{Guid.NewGuid()}")
            .Options;
        _dbContext = new MeepleAiDbContext(
            options,
            new Mock<IMediator>().Object,
            new Mock<IDomainEventCollector>().Object);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    private ResetSmokeAaronCommandHandler CreateHandler(
        bool testEndpointsEnabled = true,
        bool isProduction = false)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TestEndpoints:Enabled"] = testEndpointsEnabled.ToString().ToLowerInvariant()
            })
            .Build();

        var environment = new Mock<IWebHostEnvironment>();
        environment.Setup(e => e.EnvironmentName).Returns(isProduction ? "Production" : "Development");

        return new ResetSmokeAaronCommandHandler(
            _dbContext,
            configuration,
            environment.Object,
            NullLogger<ResetSmokeAaronCommandHandler>.Instance);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Triple-gate enforcement
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UT3_TestEndpointsDisabled_Throws()
    {
        var handler = CreateHandler(testEndpointsEnabled: false);

        var act = () => handler.Handle(new ResetSmokeAaronCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
                 .WithMessage("*disabled*");
    }

    [Fact]
    public async Task UT4_IsProductionEnvironment_Throws()
    {
        var handler = CreateHandler(testEndpointsEnabled: true, isProduction: true);

        var act = () => handler.Handle(new ResetSmokeAaronCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
                 .WithMessage("*production*");
    }

    [Fact]
    public void UT7_HardcodedTargetUserId_NotConfigurable()
    {
        // The command record has no parameters — the only way the handler can
        // ever pick a target user is the SmokeAaronUserId constant.
        var commandProperties = typeof(ResetSmokeAaronCommand).GetProperties();
        commandProperties.Should().BeEmpty(
            because: "Adding any property to ResetSmokeAaronCommand would expose " +
                     "the target-user choice to external callers, breaking the " +
                     "triple-gate's third invariant.");

        ResetSmokeAaronCommandHandler.SmokeAaronUserId
            .Should().Be(Guid.Parse("00000000-0000-4000-8000-000000005a01"));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Cascade delete
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UT5_DeletesAllSmokeAaronData_PreservingOtherUsers()
    {
        // Arrange — seed smoke-aaron data + other-user data
        var smokeAaronId = ResetSmokeAaronCommandHandler.SmokeAaronUserId;
        var otherUserId = Guid.NewGuid();

        _dbContext.PrivateGames.Add(new PrivateGameEntity { Id = Guid.NewGuid(), OwnerId = smokeAaronId, BggId = 12345 });
        _dbContext.PrivateGames.Add(new PrivateGameEntity { Id = Guid.NewGuid(), OwnerId = smokeAaronId, BggId = 67890 });
        _dbContext.PrivateGames.Add(new PrivateGameEntity { Id = Guid.NewGuid(), OwnerId = otherUserId, BggId = 11111 });

        _dbContext.KbReindexJobs.Add(new KbReindexJobEntity
        {
            Id = Guid.NewGuid(),
            GameId = Guid.NewGuid(),
            UserId = smokeAaronId,
            Status = "completed",
            TotalPdfs = 0,
            ProcessedPdfs = 0,
            CreatedAt = DateTime.UtcNow
        });
        _dbContext.KbReindexJobs.Add(new KbReindexJobEntity
        {
            Id = Guid.NewGuid(),
            GameId = Guid.NewGuid(),
            UserId = otherUserId,
            Status = "completed",
            TotalPdfs = 0,
            ProcessedPdfs = 0,
            CreatedAt = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync();

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new ResetSmokeAaronCommand(), CancellationToken.None);

        // Assert — smoke-aaron data is gone, other user untouched
        result.PrivateGames.Should().Be(2);
        result.KbReindexJobs.Should().Be(1);

        (await _dbContext.PrivateGames.CountAsync(p => p.OwnerId == smokeAaronId)).Should().Be(0);
        (await _dbContext.PrivateGames.CountAsync(p => p.OwnerId == otherUserId)).Should().Be(1);
        (await _dbContext.KbReindexJobs.CountAsync(j => j.UserId == smokeAaronId)).Should().Be(0);
        (await _dbContext.KbReindexJobs.CountAsync(j => j.UserId == otherUserId)).Should().Be(1);
    }

    [Fact]
    public async Task UT1_Idempotent_SecondCallReturnsZeros()
    {
        // Arrange — empty DB
        var handler = CreateHandler();

        // Act
        var first = await handler.Handle(new ResetSmokeAaronCommand(), CancellationToken.None);
        var second = await handler.Handle(new ResetSmokeAaronCommand(), CancellationToken.None);

        // Assert
        first.PrivateGames.Should().Be(0);
        first.ChatThreads.Should().Be(0);
        first.PdfDocuments.Should().Be(0);
        first.KbReindexJobs.Should().Be(0);

        second.Should().BeEquivalentTo(first);
    }
}
