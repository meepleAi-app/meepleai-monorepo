using Api.BoundedContexts.Administration.Application.Commands.StagingAllowlist;
using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Commands.StagingAllowlist;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
public class SeedStagingAllowlistCommandHandlerTests
{
    private static SeedStagingAllowlistCommandHandler CreateHandler(
        string environment,
        string? legacyEnvVar,
        Mock<IStagingAllowlistRepository> repository,
        Mock<IUnitOfWork> uow)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["STAGING_ALLOWED_EMAILS"] = legacyEnvVar })
            .Build();
        var env = Mock.Of<IHostEnvironment>(e => e.EnvironmentName == environment);
        return new SeedStagingAllowlistCommandHandler(
            repository.Object,
            uow.Object,
            config,
            env,
            NullLogger<SeedStagingAllowlistCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_NotStaging_SkipsCompletely()
    {
        var repo = new Mock<IStagingAllowlistRepository>(MockBehavior.Strict);
        var uow = new Mock<IUnitOfWork>(MockBehavior.Strict);
        var handler = CreateHandler("Development", null, repo, uow);

        await handler.Handle(new SeedStagingAllowlistCommand(), CancellationToken.None);

        // Strict mocks ensure no methods were called
        repo.VerifyNoOtherCalls();
        uow.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_Staging_BootstrapEmailAlreadyExists_NoInsert()
    {
        var repo = new Mock<IStagingAllowlistRepository>(MockBehavior.Strict);
        repo.Setup(r => r.ExistsByEmailAsync("badsworm@gmail.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var uow = new Mock<IUnitOfWork>(MockBehavior.Strict);

        var handler = CreateHandler("Staging", null, repo, uow);
        await handler.Handle(new SeedStagingAllowlistCommand(), CancellationToken.None);

        repo.Verify(r => r.AddAsync(It.IsAny<StagingAllowlistEntry>(), It.IsAny<CancellationToken>()), Times.Never);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Staging_NewBootstrapEmail_InsertsAndSaves()
    {
        var repo = new Mock<IStagingAllowlistRepository>(MockBehavior.Strict);
        repo.Setup(r => r.ExistsByEmailAsync("badsworm@gmail.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        repo.Setup(r => r.AddAsync(It.IsAny<StagingAllowlistEntry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var uow = new Mock<IUnitOfWork>(MockBehavior.Strict);
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = CreateHandler("Staging", null, repo, uow);
        await handler.Handle(new SeedStagingAllowlistCommand(), CancellationToken.None);

        repo.Verify(r => r.AddAsync(
            It.Is<StagingAllowlistEntry>(e => e.Email == "badsworm@gmail.com" && e.AddedByUserId == null),
            It.IsAny<CancellationToken>()), Times.Once);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Staging_LegacyEnvVarEmailsMigrated_Idempotent()
    {
        var repo = new Mock<IStagingAllowlistRepository>(MockBehavior.Strict);
        repo.Setup(r => r.ExistsByEmailAsync("badsworm@gmail.com", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repo.Setup(r => r.ExistsByEmailAsync("legacy1@example.com", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        repo.Setup(r => r.ExistsByEmailAsync("legacy2@example.com", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repo.Setup(r => r.AddAsync(It.IsAny<StagingAllowlistEntry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var uow = new Mock<IUnitOfWork>(MockBehavior.Strict);
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = CreateHandler("Staging",
            legacyEnvVar: "Legacy1@Example.com,legacy2@example.com", repo, uow);
        await handler.Handle(new SeedStagingAllowlistCommand(), CancellationToken.None);

        repo.Verify(r => r.AddAsync(
            It.Is<StagingAllowlistEntry>(e => e.Email == "legacy1@example.com"),
            It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.AddAsync(
            It.Is<StagingAllowlistEntry>(e => e.Email == "legacy2@example.com"),
            It.IsAny<CancellationToken>()), Times.Never,
            "legacy2 already exists — no duplicate insert");

        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once,
            "single SaveChanges only when at least one row inserted");
    }
}
