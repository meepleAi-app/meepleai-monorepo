using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Application.Validators;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public class UpdateHandSlotCommandValidatorTests
{
    private readonly UpdateHandSlotCommandValidator _sut = new();

    [Theory]
    [InlineData("toolkit", "toolkit")]
    [InlineData("game", "game")]
    [InlineData("session", "session")]
    [InlineData("ai", "agent")]
    public void Valid_combinations_pass(string slotType, string entityType)
    {
        var cmd = new UpdateHandSlotCommand(Guid.NewGuid(), slotType, Guid.NewGuid(), entityType);
        var result = _sut.TestValidate(cmd);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("toolkit", "game")]
    [InlineData("game", "agent")]
    [InlineData("session", "toolkit")]
    [InlineData("ai", "game")]
    public void Incompatible_slot_entityType_fails(string slotType, string entityType)
    {
        var cmd = new UpdateHandSlotCommand(Guid.NewGuid(), slotType, Guid.NewGuid(), entityType);
        var result = _sut.TestValidate(cmd);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void Empty_UserId_fails()
    {
        var cmd = new UpdateHandSlotCommand(Guid.Empty, "toolkit", Guid.NewGuid(), "toolkit");
        var result = _sut.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.UserId);
    }

    [Fact]
    public void Invalid_slotType_fails()
    {
        var cmd = new UpdateHandSlotCommand(Guid.NewGuid(), "unknown", Guid.NewGuid(), "toolkit");
        var result = _sut.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.SlotType);
    }

    [Fact]
    public void Empty_EntityId_fails()
    {
        var cmd = new UpdateHandSlotCommand(Guid.NewGuid(), "toolkit", Guid.Empty, "toolkit");
        var result = _sut.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.EntityId);
    }
}
