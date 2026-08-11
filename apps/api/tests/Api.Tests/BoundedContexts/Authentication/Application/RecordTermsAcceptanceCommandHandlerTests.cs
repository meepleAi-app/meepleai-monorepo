using System;
using System.Threading;
using System.Threading.Tasks;
using Api.BoundedContexts.Authentication.Application.Commands;
using Api.BoundedContexts.Authentication.Domain.Constants;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Enums;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application;

[Trait("Category", TestCategories.Unit)]
public sealed class RecordTermsAcceptanceCommandHandlerTests
{
    private readonly Mock<ITermsAcceptanceRepository> _repo = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private RecordTermsAcceptanceCommandHandler CreateSut() => new(_repo.Object, _uow.Object);

    [Fact]
    public async Task Handle_NoPriorAcceptance_AppendsCurrentVersion()
    {
        _repo.Setup(r => r.GetLatestByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TermsAcceptance?)null);

        var result = await CreateSut().Handle(
            new RecordTermsAcceptanceCommand(Guid.NewGuid()), CancellationToken.None);

        _repo.Verify(r => r.AddAsync(
            It.Is<TermsAcceptance>(a => a.TermsVersion == TermsVersion.Current
                                        && a.Context == TermsAcceptanceContext.ReConsent),
            It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        result.AcceptedVersion.Should().Be(TermsVersion.Current);
        result.NeedsReAcceptance.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_AlreadyAcceptedCurrent_IsNoOp()
    {
        var userId = Guid.NewGuid();
        _repo.Setup(r => r.GetLatestByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TermsAcceptance.Create(userId, TermsVersion.Current, TermsAcceptanceContext.Registration));

        var result = await CreateSut().Handle(
            new RecordTermsAcceptanceCommand(userId), CancellationToken.None);

        _repo.Verify(r => r.AddAsync(It.IsAny<TermsAcceptance>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        result.NeedsReAcceptance.Should().BeFalse();
        result.AcceptedVersion.Should().Be(TermsVersion.Current);
    }

    [Fact]
    public async Task Handle_StalePriorAcceptance_AppendsNewRow()
    {
        var userId = Guid.NewGuid();
        _repo.Setup(r => r.GetLatestByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TermsAcceptance.Create(userId, "2026-03-09", TermsAcceptanceContext.Registration));

        await CreateSut().Handle(new RecordTermsAcceptanceCommand(userId), CancellationToken.None);

        _repo.Verify(r => r.AddAsync(It.IsAny<TermsAcceptance>(), It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
