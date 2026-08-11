using System;
using System.Threading;
using System.Threading.Tasks;
using Api.BoundedContexts.Authentication.Application.Queries;
using Api.BoundedContexts.Authentication.Domain.Constants;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Enums;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application;

[Trait("Category", TestCategories.Unit)]
public sealed class GetTermsConsentStatusQueryHandlerTests
{
    private readonly Mock<ITermsAcceptanceRepository> _repo = new();

    private GetTermsConsentStatusQueryHandler CreateSut() => new(_repo.Object);

    [Fact]
    public async Task Handle_NoAcceptance_NeedsReAcceptanceTrue()
    {
        _repo.Setup(r => r.GetLatestByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TermsAcceptance?)null);

        var result = await CreateSut().Handle(new GetTermsConsentStatusQuery(Guid.NewGuid()), CancellationToken.None);

        result.CurrentVersion.Should().Be(TermsVersion.Current);
        result.AcceptedVersion.Should().BeNull();
        result.AcceptedAt.Should().BeNull();
        result.NeedsReAcceptance.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_StaleVersion_NeedsReAcceptanceTrue()
    {
        var userId = Guid.NewGuid();
        _repo.Setup(r => r.GetLatestByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TermsAcceptance.Create(userId, "2026-03-09", TermsAcceptanceContext.Registration));

        var result = await CreateSut().Handle(new GetTermsConsentStatusQuery(userId), CancellationToken.None);

        result.AcceptedVersion.Should().Be("2026-03-09");
        result.NeedsReAcceptance.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_CurrentVersion_NeedsReAcceptanceFalse()
    {
        var userId = Guid.NewGuid();
        _repo.Setup(r => r.GetLatestByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TermsAcceptance.Create(userId, TermsVersion.Current, TermsAcceptanceContext.ReConsent));

        var result = await CreateSut().Handle(new GetTermsConsentStatusQuery(userId), CancellationToken.None);

        result.AcceptedVersion.Should().Be(TermsVersion.Current);
        result.NeedsReAcceptance.Should().BeFalse();
    }
}
