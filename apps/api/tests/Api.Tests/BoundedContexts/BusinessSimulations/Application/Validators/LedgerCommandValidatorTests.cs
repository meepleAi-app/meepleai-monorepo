using Api.BoundedContexts.BusinessSimulations.Application.Commands;
using Api.BoundedContexts.BusinessSimulations.Domain.Enums;
using Api.Tests.Constants;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.BusinessSimulations.Application.Validators;

/// <summary>
/// Unit tests for ledger CRUD command validators (Issue #3722)
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "BusinessSimulations")]
public sealed class LedgerCommandValidatorTests
{
    #region CreateManualLedgerEntryCommandValidator

    private readonly CreateManualLedgerEntryCommandValidator _createValidator = new();

    [Fact]
    public void Create_ValidCommand_ShouldPassValidation()
    {
        var command = new CreateManualLedgerEntryCommand(
            DateTime.UtcNow, LedgerEntryType.Income, LedgerCategory.Subscription,
            99.99m, "EUR", "Test", Guid.NewGuid());

        var result = _createValidator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Create_FutureDate_ShouldFailValidation()
    {
        var command = new CreateManualLedgerEntryCommand(
            DateTime.UtcNow.AddDays(5), LedgerEntryType.Income, LedgerCategory.Subscription,
            10m, "EUR", null, Guid.NewGuid());

        var result = _createValidator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Date);
    }

    [Fact]
    public void Create_ZeroAmount_ShouldFailValidation()
    {
        var command = new CreateManualLedgerEntryCommand(
            DateTime.UtcNow, LedgerEntryType.Income, LedgerCategory.Subscription,
            0m, "EUR", null, Guid.NewGuid());

        var result = _createValidator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Amount);
    }

    [Fact]
    public void Create_NegativeAmount_ShouldFailValidation()
    {
        var command = new CreateManualLedgerEntryCommand(
            DateTime.UtcNow, LedgerEntryType.Income, LedgerCategory.Subscription,
            -50m, "EUR", null, Guid.NewGuid());

        var result = _createValidator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Amount);
    }

    [Fact]
    public void Create_EmptyCurrency_ShouldFailValidation()
    {
        var command = new CreateManualLedgerEntryCommand(
            DateTime.UtcNow, LedgerEntryType.Income, LedgerCategory.Subscription,
            10m, "", null, Guid.NewGuid());

        var result = _createValidator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Currency);
    }

    [Fact]
    public void Create_InvalidCurrencyFormat_ShouldFailValidation()
    {
        var command = new CreateManualLedgerEntryCommand(
            DateTime.UtcNow, LedgerEntryType.Income, LedgerCategory.Subscription,
            10m, "eu", null, Guid.NewGuid());

        var result = _createValidator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Currency);
    }

    [Fact]
    public void Create_LowercaseCurrency_ShouldFailValidation()
    {
        var command = new CreateManualLedgerEntryCommand(
            DateTime.UtcNow, LedgerEntryType.Income, LedgerCategory.Subscription,
            10m, "eur", null, Guid.NewGuid());

        var result = _createValidator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Currency);
    }

    [Fact]
    public void Create_DescriptionTooLong_ShouldFailValidation()
    {
        var command = new CreateManualLedgerEntryCommand(
            DateTime.UtcNow, LedgerEntryType.Income, LedgerCategory.Subscription,
            10m, "EUR", new string('x', 501), Guid.NewGuid());

        var result = _createValidator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Create_EmptyUserId_ShouldFailValidation()
    {
        var command = new CreateManualLedgerEntryCommand(
            DateTime.UtcNow, LedgerEntryType.Income, LedgerCategory.Subscription,
            10m, "EUR", null, Guid.Empty);

        var result = _createValidator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.CreatedByUserId);
    }

    [Fact]
    public void Create_NullDescription_ShouldPassValidation()
    {
        var command = new CreateManualLedgerEntryCommand(
            DateTime.UtcNow, LedgerEntryType.Income, LedgerCategory.Subscription,
            10m, "EUR", null, Guid.NewGuid());

        var result = _createValidator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.Description);
    }

    #endregion

    #region UpdateLedgerEntryCommandValidator

    private readonly UpdateLedgerEntryCommandValidator _updateValidator = new();

    [Fact]
    public void Update_ValidCommand_ShouldPassValidation()
    {
        var command = new UpdateLedgerEntryCommand(
            Guid.NewGuid(), "New desc", LedgerCategory.Marketing, null);

        var result = _updateValidator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Update_EmptyId_ShouldFailValidation()
    {
        var command = new UpdateLedgerEntryCommand(
            Guid.Empty, "desc", null, null);

        var result = _updateValidator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Id);
    }

    [Fact]
    public void Update_DescriptionTooLong_ShouldFailValidation()
    {
        var command = new UpdateLedgerEntryCommand(
            Guid.NewGuid(), new string('x', 501), null, null);

        var result = _updateValidator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Update_MetadataTooLong_ShouldFailValidation()
    {
        var command = new UpdateLedgerEntryCommand(
            Guid.NewGuid(), null, null, new string('x', 4001));

        var result = _updateValidator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Metadata);
    }

    [Fact]
    public void Update_AllNull_ShouldPassValidation()
    {
        var command = new UpdateLedgerEntryCommand(
            Guid.NewGuid(), null, null, null);

        var result = _updateValidator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    #endregion

    #region DeleteLedgerEntryCommandValidator

    private readonly DeleteLedgerEntryCommandValidator _deleteValidator = new();

    [Fact]
    public void Delete_ValidId_ShouldPassValidation()
    {
        var command = new DeleteLedgerEntryCommand(Guid.NewGuid());
        var result = _deleteValidator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Delete_EmptyId_ShouldFailValidation()
    {
        var command = new DeleteLedgerEntryCommand(Guid.Empty);
        var result = _deleteValidator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Id);
    }

    #endregion
}
