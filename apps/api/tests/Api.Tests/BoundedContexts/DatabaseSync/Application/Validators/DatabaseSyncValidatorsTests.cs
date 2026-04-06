using Api.BoundedContexts.DatabaseSync.Application.Commands;
using Api.BoundedContexts.DatabaseSync.Domain.Enums;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.DatabaseSync.Application.Validators;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DatabaseSync")]
public sealed class DatabaseSyncValidatorsTests
{
    // ── ApplyMigrationsCommandValidator ───────────────────────────────────────

    private readonly ApplyMigrationsCommandValidator _applyValidator = new();

    [Fact]
    public void ApplyMigrationsCommand_ValidCommand_PassesValidation()
    {
        var cmd = new ApplyMigrationsCommand(
            SyncDirection.StagingToLocal,
            "CONFIRM",
            Guid.NewGuid());

        var result = _applyValidator.TestValidate(cmd);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ApplyMigrationsCommand_EmptyConfirmation_FailsValidation()
    {
        var cmd = new ApplyMigrationsCommand(SyncDirection.StagingToLocal, "", Guid.NewGuid());

        var result = _applyValidator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.Confirmation);
    }

    [Fact]
    public void ApplyMigrationsCommand_EmptyAdminUserId_FailsValidation()
    {
        var cmd = new ApplyMigrationsCommand(SyncDirection.StagingToLocal, "CONFIRM", Guid.Empty);

        var result = _applyValidator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.AdminUserId);
    }

    [Fact]
    public void ApplyMigrationsCommand_AllValidDirections_PassValidation()
    {
        foreach (var direction in Enum.GetValues<SyncDirection>())
        {
            var cmd = new ApplyMigrationsCommand(direction, "CONFIRM", Guid.NewGuid());
            var result = _applyValidator.TestValidate(cmd);
            result.ShouldNotHaveValidationErrorFor(x => x.Direction);
        }
    }

    // ── SyncTableDataCommandValidator ─────────────────────────────────────────

    private readonly SyncTableDataCommandValidator _syncValidator = new();

    [Fact]
    public void SyncTableDataCommand_ValidCommand_PassesValidation()
    {
        var cmd = new SyncTableDataCommand("games", SyncDirection.StagingToLocal, "CONFIRM", Guid.NewGuid());

        var result = _syncValidator.TestValidate(cmd);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void SyncTableDataCommand_EmptyTableName_FailsValidation()
    {
        var cmd = new SyncTableDataCommand("", SyncDirection.StagingToLocal, "CONFIRM", Guid.NewGuid());

        var result = _syncValidator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.TableName);
    }

    [Fact]
    public void SyncTableDataCommand_WhitespaceTableName_FailsValidation()
    {
        var cmd = new SyncTableDataCommand("   ", SyncDirection.StagingToLocal, "CONFIRM", Guid.NewGuid());

        var result = _syncValidator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.TableName);
    }

    [Fact]
    public void SyncTableDataCommand_EmptyConfirmation_FailsValidation()
    {
        var cmd = new SyncTableDataCommand("games", SyncDirection.StagingToLocal, "", Guid.NewGuid());

        var result = _syncValidator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.Confirmation);
    }

    [Fact]
    public void SyncTableDataCommand_EmptyAdminUserId_FailsValidation()
    {
        var cmd = new SyncTableDataCommand("games", SyncDirection.StagingToLocal, "CONFIRM", Guid.Empty);

        var result = _syncValidator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.AdminUserId);
    }
}
