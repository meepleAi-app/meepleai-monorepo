using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Constants;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;
using SystemConfig = Api.BoundedContexts.SystemConfiguration.Domain.Entities.SystemConfiguration;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Application.Handlers;

/// <summary>
/// #4612: la riga <c>Authentication:BootstrapAdmin</c> non si crea, non si modifica, non si cancella e non si
/// esporta dall'API admin delle configurazioni. Qui ogni handler, con un repository finto: il rifiuto deve
/// arrivare prima di qualunque scrittura. La prova sul database vero, con la registrazione col token, è in
/// <c>BootstrapAdminClaimIntegrationTests</c>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SystemConfiguration")]
[Trait("Issue", "4612")]
public sealed class ReservedConfigurationKeyHandlersTests
{
    private const string BootstrapKey = "Authentication:BootstrapAdmin";

    private readonly Mock<IConfigurationRepository> _repository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Guid _userId = Guid.NewGuid();

    private SystemConfig Config(string key, string value = "true") =>
        new(Guid.NewGuid(), new ConfigKey(key), value, "bool", _userId);

    private void VerifyNothingWasWritten()
    {
        _repository.Verify(r => r.AddAsync(It.IsAny<SystemConfig>(), It.IsAny<CancellationToken>()), Times.Never);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<SystemConfig>(), It.IsAny<CancellationToken>()), Times.Never);
        _repository.Verify(r => r.DeleteAsync(It.IsAny<SystemConfig>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ----- La chiave -----

    [Fact]
    public void TheReservedKey_IsTheOneTheClaimLooksUp()
    {
        // Se qualcuno cambia la stringa, il claim cerca un'altra riga e la guardia ne protegge una che non c'è.
        ReservedConfigurationKeys.BootstrapAdmin.Should().Be(BootstrapKey);
    }

    [Theory]
    [InlineData("Authentication:BootstrapAdmin", true)]
    [InlineData("authentication:bootstrapadmin", true)]
    [InlineData("AUTHENTICATION:BOOTSTRAPADMIN", true)]
    [InlineData("  Authentication:BootstrapAdmin  ", true)]
    [InlineData("Authentication:BootstrapAdminToken", false)]
    [InlineData("Authentication:BootstrapAdmi", false)]
    [InlineData("Authentication", false)]
    [InlineData("Registration:PublicEnabled", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsReserved_MatchesOnlyTheBootstrapKey(string? key, bool expected)
    {
        ReservedConfigurationKeys.IsReserved(key).Should().Be(expected);
    }

    // ----- Scritture dirette -----

    [Fact]
    public async Task Update_OfTheReservedRow_ThrowsForbidden_AndWritesNothing()
    {
        var config = Config(BootstrapKey);
        _repository.Setup(r => r.GetByIdAsync(config.Id, It.IsAny<CancellationToken>())).ReturnsAsync(config);
        var handler = new UpdateConfigValueCommandHandler(_repository.Object, _unitOfWork.Object);

        var act = async () => await handler.Handle(
            new UpdateConfigValueCommand(config.Id, "false", _userId), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ForbiddenException>();
        config.Value.Should().Be("true");
        VerifyNothingWasWritten();
    }

    [Fact]
    public async Task Toggle_OfTheReservedRow_ThrowsForbidden_AndWritesNothing()
    {
        var config = Config(BootstrapKey);
        _repository.Setup(r => r.GetByIdAsync(config.Id, It.IsAny<CancellationToken>())).ReturnsAsync(config);
        var handler = new ToggleConfigurationCommandHandler(_repository.Object, _unitOfWork.Object);

        var act = async () => await handler.Handle(
            new ToggleConfigurationCommand(config.Id, IsActive: false), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ForbiddenException>();
        config.IsActive.Should().BeTrue();
        VerifyNothingWasWritten();
    }

    [Fact]
    public async Task Delete_OfTheReservedRow_ThrowsForbidden_AndWritesNothing()
    {
        var config = Config(BootstrapKey);
        _repository.Setup(r => r.GetByIdAsync(config.Id, It.IsAny<CancellationToken>())).ReturnsAsync(config);
        var handler = new DeleteConfigurationCommandHandler(_repository.Object, _unitOfWork.Object);

        var act = async () => await handler.Handle(
            new DeleteConfigurationCommand(config.Id), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ForbiddenException>();
        config.DomainEvents.OfType<Api.BoundedContexts.SystemConfiguration.Domain.Events.ConfigurationDeletedEvent>()
            .Should().BeEmpty("the row was refused before MarkAsDeleted");
        VerifyNothingWasWritten();
    }

    [Fact]
    public async Task Rollback_OfTheReservedRow_ThrowsForbidden_AndWritesNothing()
    {
        var config = Config(BootstrapKey);
        config.UpdateValue("false", _userId);
        config.ClearDomainEvents();
        _repository.Setup(r => r.GetByIdAsync(config.Id, It.IsAny<CancellationToken>())).ReturnsAsync(config);
        var handler = new RollbackConfigCommandHandler(_repository.Object, _unitOfWork.Object);

        var act = async () => await handler.Handle(
            new RollbackConfigCommand(config.Id, TargetVersion: 1, _userId), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ForbiddenException>();
        config.Value.Should().Be("false", "the rollback did not run");
        VerifyNothingWasWritten();
    }

    [Fact]
    public async Task Create_OfTheReservedKey_ThrowsForbidden_AndWritesNothing()
    {
        var handler = new CreateConfigurationCommandHandler(_repository.Object, _unitOfWork.Object);

        var act = async () => await handler.Handle(
            new CreateConfigurationCommand(
                Key: BootstrapKey,
                Value: "true",
                ValueType: "bool",
                Description: null,
                Category: "Authentication",
                Environment: "All",
                RequiresRestart: false,
                CreatedByUserId: _userId),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ForbiddenException>();
        VerifyNothingWasWritten();
    }

    [Fact]
    public async Task BulkUpdate_WithTheReservedRowAmongOthers_ThrowsForbidden_AndChangesNoRow()
    {
        var ordinary = Config("Test4612:Ordinary", value: "uno");
        var reserved = Config(BootstrapKey);
        _repository
            .Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([ordinary, reserved]);
        var handler = new BulkUpdateConfigsCommandHandler(_repository.Object, _unitOfWork.Object);

        var act = async () => await handler.Handle(
            new BulkUpdateConfigsCommand(
                [new ConfigurationUpdate(ordinary.Id, "due"), new ConfigurationUpdate(reserved.Id, "false")],
                _userId),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ForbiddenException>();
        ordinary.Value.Should().Be("uno", "one refused item refuses the whole request, ordinary rows included");
        VerifyNothingWasWritten();
    }

    // ----- Import ed export -----

    private void SetupExisting(params SystemConfig[] existing) =>
        _repository
            .Setup(r => r.GetByKeysAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string?>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<SystemConfig>)existing.ToList());

    private static ConfigurationImportItem Item(string key, string value = "x") =>
        new(key, value, "string", null, "General", true, false, "All");

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Import_OfTheReservedKey_WhenTheRowExists_SkipsItWhateverTheOverwriteFlag(bool overwrite)
    {
        var reserved = Config(BootstrapKey);
        SetupExisting(reserved);
        var handler = new ImportConfigsCommandHandler(_repository.Object, _unitOfWork.Object);

        var result = await handler.Handle(
            new ImportConfigsCommand([Item(BootstrapKey, "false")], overwrite, _userId),
            TestContext.Current.CancellationToken);

        result.ImportedCount.Should().Be(0);
        result.SkippedKeys.Should().Equal(BootstrapKey);
        reserved.Value.Should().Be("true");
        _repository.Verify(r => r.UpdateAsync(It.IsAny<SystemConfig>(), It.IsAny<CancellationToken>()), Times.Never);
        _repository.Verify(r => r.AddAsync(It.IsAny<SystemConfig>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Import_OfTheReservedKey_WhenTheRowIsAbsent_DoesNotCreateIt()
    {
        SetupExisting();
        var handler = new ImportConfigsCommandHandler(_repository.Object, _unitOfWork.Object);

        var result = await handler.Handle(
            new ImportConfigsCommand([Item("  authentication:BOOTSTRAPADMIN ")], OverwriteExisting: false, _userId),
            TestContext.Current.CancellationToken);

        result.ImportedCount.Should().Be(0);
        result.SkippedKeys.Should().ContainSingle();
        _repository.Verify(r => r.AddAsync(It.IsAny<SystemConfig>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Import_WithTheReservedKeyAmongOthers_ImportsTheOthers()
    {
        var existing = Config("Test4612:Existing", value: "uno");
        SetupExisting(existing);
        var handler = new ImportConfigsCommandHandler(_repository.Object, _unitOfWork.Object);

        var result = await handler.Handle(
            new ImportConfigsCommand(
                [Item("Test4612:Existing", "due"), Item(BootstrapKey), Item("Test4612:New")],
                OverwriteExisting: true,
                _userId),
            TestContext.Current.CancellationToken);

        result.ImportedCount.Should().Be(2, "the existing row is overwritten and the new one is added");
        result.SkippedKeys.Should().Equal(BootstrapKey);
        existing.Value.Should().Be("due");
        _repository.Verify(r => r.AddAsync(
            It.Is<SystemConfig>(c => c.Key.Value == "Test4612:New"), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Export_DoesNotCarryTheReservedRow()
    {
        var ordinary = Config("Test4612:Ordinary");
        var reserved = Config(BootstrapKey);
        _repository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<SystemConfig>)[ordinary, reserved]);
        var handler = new ExportConfigsQueryHandler(_repository.Object);

        var export = await handler.Handle(
            new ExportConfigsQuery(Environment: "Production", ActiveOnly: false), TestContext.Current.CancellationToken);

        export.Configurations.Select(c => c.Key).Should().Equal("Test4612:Ordinary");
    }

    // ----- Controllo -----

    [Fact]
    public async Task Update_OfAnOrdinaryRow_StillWorks()
    {
        var config = Config("Test4612:Ordinary", value: "uno");
        _repository.Setup(r => r.GetByIdAsync(config.Id, It.IsAny<CancellationToken>())).ReturnsAsync(config);
        var handler = new UpdateConfigValueCommandHandler(_repository.Object, _unitOfWork.Object);

        var result = await handler.Handle(
            new UpdateConfigValueCommand(config.Id, "due", _userId), TestContext.Current.CancellationToken);

        result.Value.Should().Be("due");
        _repository.Verify(r => r.UpdateAsync(config, It.IsAny<CancellationToken>()), Times.Once);
    }
}
