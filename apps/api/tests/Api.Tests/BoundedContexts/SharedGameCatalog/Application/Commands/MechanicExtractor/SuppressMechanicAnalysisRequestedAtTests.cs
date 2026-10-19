using Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

/// <summary>
/// #4519 — <c>RequestedAt</c> della soppressione arriva all'aggregato come lo stesso istante in UTC,
/// qualunque sia il suo <see cref="DateTimeKind"/>.
/// </summary>
/// <remarks>
/// Prima l'handler faceva <c>SpecifyKind(…, Utc)</c> su ogni valore, che su un
/// <see cref="DateTimeKind.Local"/> sposta l'istante; per questo il validatore rifiutava
/// <c>Local</c> con «RequestedAt must be expressed in UTC». Da #4519 il corpo HTTP non porta più un
/// <c>Local</c> (lo converte <c>UtcDateTimeJson</c>), e l'handler converte correttamente anche quello:
/// la regola del validatore non protegge più nulla ed è stata tolta.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class SuppressMechanicAnalysisRequestedAtTests
{
    private static SuppressMechanicAnalysisCommand Command(DateTime? requestedAt) => new(
        AnalysisId: Guid.NewGuid(),
        ActorId: Guid.NewGuid(),
        Reason: "#4519 takedown request received by email",
        RequestSource: SuppressionRequestSource.Email,
        RequestedAt: requestedAt);

    [Theory]
    [InlineData(DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Unspecified)]
    [InlineData(DateTimeKind.Local)]
    public void Validator_AcceptsRequestedAtOfAnyKind(DateTimeKind kind)
    {
        var result = new SuppressMechanicAnalysisCommandValidator()
            .Validate(Command(new DateTime(2026, 1, 1, 10, 0, 0, kind)));

        result.IsValid.Should().BeTrue(string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }

    public static TheoryData<DateTime, DateTime> RequestedAtAndStoredUtc()
    {
        var instant = new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc);
        return new TheoryData<DateTime, DateTime>
        {
            { instant, instant },
            // Senza fuso: l'orologio scritto resta quello, il fuso è UTC (la regola di #4063).
            { new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Unspecified), instant },
            // Local nomina già un istante: va convertito, non rimarcato.
            { instant.ToLocalTime(), instant },
        };
    }

    [Theory]
    [MemberData(nameof(RequestedAtAndStoredUtc))]
    public async Task Handler_StoresTheSameInstantInUtc(DateTime requestedAt, DateTime expectedUtc)
    {
        var analysis = MechanicAnalysis.Create(
            sharedGameId: Guid.NewGuid(),
            pdfDocumentId: Guid.NewGuid(),
            promptVersion: "v1",
            createdBy: Guid.NewGuid(),
            createdAt: DateTime.UtcNow,
            modelUsed: "deepseek-chat",
            provider: "deepseek",
            costCapUsd: 1m);

        var repository = new Mock<IMechanicAnalysisRepository>();
        repository
            .Setup(r => r.GetByIdIgnoringFiltersAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(analysis);

        var handler = new SuppressMechanicAnalysisCommandHandler(
            repository.Object,
            Mock.Of<IUnitOfWork>(),
            TimeProvider.System,
            NullLogger<SuppressMechanicAnalysisCommandHandler>.Instance);

        await handler.Handle(Command(requestedAt), CancellationToken.None);

        analysis.SuppressionRequestedAt.Should().Be(expectedUtc);
        analysis.SuppressionRequestedAt!.Value.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public async Task Handler_NullRequestedAt_StaysNull()
    {
        var analysis = MechanicAnalysis.Create(
            sharedGameId: Guid.NewGuid(),
            pdfDocumentId: Guid.NewGuid(),
            promptVersion: "v1",
            createdBy: Guid.NewGuid(),
            createdAt: DateTime.UtcNow,
            modelUsed: "deepseek-chat",
            provider: "deepseek",
            costCapUsd: 1m);

        var repository = new Mock<IMechanicAnalysisRepository>();
        repository
            .Setup(r => r.GetByIdIgnoringFiltersAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(analysis);

        var handler = new SuppressMechanicAnalysisCommandHandler(
            repository.Object,
            Mock.Of<IUnitOfWork>(),
            TimeProvider.System,
            NullLogger<SuppressMechanicAnalysisCommandHandler>.Instance);

        await handler.Handle(Command(null), CancellationToken.None);

        analysis.SuppressionRequestedAt.Should().BeNull();
    }
}
