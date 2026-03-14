using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.Queries;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Validators;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SessionTracking")]
public class SessionCheckpointValidatorTests
{
    private readonly CreateSessionCheckpointCommandValidator _createValidator = new();
    private readonly RestoreSessionCheckpointCommandValidator _restoreValidator = new();
    private readonly ListSessionCheckpointsQueryValidator _listValidator = new();

    [Fact] public void Create_Valid_Passes() =>
        _createValidator.TestValidate(new CreateSessionCheckpointCommand(Guid.NewGuid(), Guid.NewGuid(), "Test")).ShouldNotHaveAnyValidationErrors();

    [Fact] public void Create_EmptySession_Fails() =>
        _createValidator.TestValidate(new CreateSessionCheckpointCommand(Guid.Empty, Guid.NewGuid(), "Test")).ShouldHaveValidationErrorFor(x => x.SessionId);

    [Fact] public void Create_EmptyRequester_Fails() =>
        _createValidator.TestValidate(new CreateSessionCheckpointCommand(Guid.NewGuid(), Guid.Empty, "Test")).ShouldHaveValidationErrorFor(x => x.RequesterId);

    [Fact] public void Create_EmptyName_Fails() =>
        _createValidator.TestValidate(new CreateSessionCheckpointCommand(Guid.NewGuid(), Guid.NewGuid(), "")).ShouldHaveValidationErrorFor(x => x.Name);

    [Fact] public void Create_LongName_Fails() =>
        _createValidator.TestValidate(new CreateSessionCheckpointCommand(Guid.NewGuid(), Guid.NewGuid(), new string('A', 201))).ShouldHaveValidationErrorFor(x => x.Name);

    [Fact] public void Create_MaxName_Passes() =>
        _createValidator.TestValidate(new CreateSessionCheckpointCommand(Guid.NewGuid(), Guid.NewGuid(), new string('A', 200))).ShouldNotHaveAnyValidationErrors();

    [Fact] public void Restore_Valid_Passes() =>
        _restoreValidator.TestValidate(new RestoreSessionCheckpointCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid())).ShouldNotHaveAnyValidationErrors();

    [Fact] public void Restore_EmptySession_Fails() =>
        _restoreValidator.TestValidate(new RestoreSessionCheckpointCommand(Guid.Empty, Guid.NewGuid(), Guid.NewGuid())).ShouldHaveValidationErrorFor(x => x.SessionId);

    [Fact] public void Restore_EmptyRequester_Fails() =>
        _restoreValidator.TestValidate(new RestoreSessionCheckpointCommand(Guid.NewGuid(), Guid.Empty, Guid.NewGuid())).ShouldHaveValidationErrorFor(x => x.RequesterId);

    [Fact] public void Restore_EmptyCheckpoint_Fails() =>
        _restoreValidator.TestValidate(new RestoreSessionCheckpointCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty)).ShouldHaveValidationErrorFor(x => x.CheckpointId);

    [Fact] public void List_Valid_Passes() =>
        _listValidator.TestValidate(new ListSessionCheckpointsQuery(Guid.NewGuid(), Guid.NewGuid())).ShouldNotHaveAnyValidationErrors();

    [Fact] public void List_EmptySession_Fails() =>
        _listValidator.TestValidate(new ListSessionCheckpointsQuery(Guid.Empty, Guid.NewGuid())).ShouldHaveValidationErrorFor(x => x.SessionId);

    [Fact] public void List_EmptyRequester_Fails() =>
        _listValidator.TestValidate(new ListSessionCheckpointsQuery(Guid.NewGuid(), Guid.Empty)).ShouldHaveValidationErrorFor(x => x.RequesterId);
}
