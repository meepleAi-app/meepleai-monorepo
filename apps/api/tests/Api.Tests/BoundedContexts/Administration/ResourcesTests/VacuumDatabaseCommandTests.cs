using Api.BoundedContexts.Administration.Application.Commands.Resources;
using Api.Infrastructure;
using Api.Tests.Infrastructure;
using FluentAssertions;
using FluentValidation.TestHelper;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.ResourcesTests;

/// <summary>
/// Integration tests for VACUUM database command.
/// Issue #3695: Resources Monitoring - VACUUM database action
/// </summary>
[Collection("SharedTestcontainers")]
[Trait("Category", "Integration")]
[Trait("BoundedContext", "Administration")]
[Trait("Epic", "3685")]
public class VacuumDatabaseCommandTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private ServiceProvider? _serviceProvider;

    public VacuumDatabaseCommandTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        // Issue #1628 follow-up: use the shared Postgres container with an isolated database
        // instead of spinning up a dedicated container per test class. VACUUM is per-database.
        var dbName = $"test_vacuum_{Guid.NewGuid():N}";
        var connStr = await _fixture.CreateIsolatedDatabaseAsync(dbName).ConfigureAwait(false);

        var services = new ServiceCollection();
        services.AddDbContext<MeepleAiDbContext>(options =>
            options.UseNpgsql(connStr, o => o.UseVector()));

        services.AddScoped<IMediator>(_ => Mock.Of<IMediator>());
        services.AddScoped<Api.SharedKernel.Application.Services.IDomainEventCollector>(_ => Mock.Of<Api.SharedKernel.Application.Services.IDomainEventCollector>());

        _serviceProvider = services.BuildServiceProvider();

        // Apply migrations on the freshly-created empty database (pre-flight Q2).
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        await db.Database.MigrateAsync().ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_serviceProvider != null)
        {
            await _serviceProvider.DisposeAsync().ConfigureAwait(false);
        }
    }

    [Fact]
    public async Task Handle_WithConfirmation_ExecutesVacuumSuccessfully()
    {
        // Arrange
        using var scope = _serviceProvider!.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var handler = new VacuumDatabaseCommandHandler(db);
        var command = new VacuumDatabaseCommand(Confirmed: true, FullVacuum: false);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithFullVacuum_ExecutesSuccessfully()
    {
        // Arrange
        using var scope = _serviceProvider!.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var handler = new VacuumDatabaseCommandHandler(db);
        var command = new VacuumDatabaseCommand(Confirmed: true, FullVacuum: true);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithoutConfirmation_ThrowsInvalidOperationException()
    {
        // Arrange
        using var scope = _serviceProvider!.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var handler = new VacuumDatabaseCommandHandler(db);
        var command = new VacuumDatabaseCommand(Confirmed: false);

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Confirmation is required*");
    }

    [Fact]
    public async Task Handle_WithNullCommand_ThrowsArgumentNullException()
    {
        // Arrange
        using var scope = _serviceProvider!.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var handler = new VacuumDatabaseCommandHandler(db);

        // Act
        var act = () => handler.Handle(null!, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullDb_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new VacuumDatabaseCommandHandler(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Validator_WithoutConfirmation_HasValidationError()
    {
        // Arrange
        var validator = new VacuumDatabaseCommandValidator();
        var command = new VacuumDatabaseCommand(Confirmed: false);

        // Act
        var result = validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Confirmed)
            .WithErrorMessage("Confirmation is required to execute VACUUM. This operation will briefly lock tables.");
    }

    [Fact]
    public void Validator_WithConfirmation_PassesValidation()
    {
        // Arrange
        var validator = new VacuumDatabaseCommandValidator();
        var command = new VacuumDatabaseCommand(Confirmed: true);

        // Act
        var result = validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Confirmed);
    }
}
