using Api.BoundedContexts.Authentication.Application.Commands;
using Api.BoundedContexts.Authentication.Application.Validators;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application.Validators;

/// <summary>
/// #4157 — ogni campo di <see cref="UpdatePreferencesCommand"/> è opzionale; uno presente si valida
/// come prima. La pagina delle preferenze manda solo tema, lingua e notifiche, e riceveva 422 su
/// <c>DataRetentionDays</c>, che non mandava.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
[Trait("Issue", "4157")]
public sealed class UpdatePreferencesCommandValidatorTests
{
    private readonly UpdatePreferencesCommandValidator _validator = new();

    [Fact]
    public void TheBodyOfThePreferencesPage_IsValid()
    {
        var command = new UpdatePreferencesCommand
        {
            UserId = Guid.NewGuid(),
            Theme = "system",
            Language = "it",
            EmailNotifications = true,
        };

        _validator.TestValidate(command).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ARequestWithOnlyTheUser_IsValid()
    {
        _validator.TestValidate(new UpdatePreferencesCommand { UserId = Guid.NewGuid() })
            .ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3651)]
    public void AnExplicitDataRetentionOutOfRange_IsStillRejected(int days)
    {
        var command = new UpdatePreferencesCommand { UserId = Guid.NewGuid(), DataRetentionDays = days };

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.DataRetentionDays);
    }

    [Fact]
    public void AnExplicitEmptyLanguage_IsStillRejected()
    {
        var command = new UpdatePreferencesCommand { UserId = Guid.NewGuid(), Language = "" };

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Language);
    }

    [Fact]
    public void AnExplicitEmptyTheme_IsStillRejected()
    {
        var command = new UpdatePreferencesCommand { UserId = Guid.NewGuid(), Theme = "" };

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Theme);
    }

    [Fact]
    public void AMissingUser_IsStillRejected()
    {
        _validator.TestValidate(new UpdatePreferencesCommand { UserId = Guid.Empty })
            .ShouldHaveValidationErrorFor(x => x.UserId);
    }
}
