using Api.BoundedContexts.SharedGameCatalog.Application.Commands.EnrichCatalogCover;
using Api.BoundedContexts.SharedGameCatalog.Application.Validators;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Validators;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "1823")]
public class AdminBulkAcknowledgeWikidataCoverCommandValidatorTests
{
    private readonly AdminBulkAcknowledgeWikidataCoverCommandValidator _sut = new();
    private readonly Guid _userId = Guid.NewGuid();

    [Fact]
    public async Task EmptyAttemptIds_Fails()
    {
        var cmd = new AdminBulkAcknowledgeWikidataCoverCommand(
            AttemptIds: Array.Empty<Guid>(),
            Note: null,
            TriggeredByUserId: _userId);

        var r = await _sut.TestValidateAsync(cmd);
        r.ShouldHaveValidationErrorFor(c => c.AttemptIds);
    }

    [Fact]
    public async Task OverMaxBatchSize_Fails()
    {
        var ids = Enumerable.Range(0, AdminBulkAcknowledgeWikidataCoverCommandValidator.MaxBatchSize + 1)
            .Select(_ => Guid.NewGuid()).ToList();
        var cmd = new AdminBulkAcknowledgeWikidataCoverCommand(ids, null, _userId);

        var r = await _sut.TestValidateAsync(cmd);
        r.ShouldHaveValidationErrorFor(c => c.AttemptIds)
            .WithErrorMessage($"AttemptIds cannot exceed {AdminBulkAcknowledgeWikidataCoverCommandValidator.MaxBatchSize} per request.");
    }

    [Fact]
    public async Task ContainsGuidEmpty_Fails()
    {
        var cmd = new AdminBulkAcknowledgeWikidataCoverCommand(
            AttemptIds: new[] { Guid.NewGuid(), Guid.Empty },
            Note: null,
            TriggeredByUserId: _userId);

        var r = await _sut.TestValidateAsync(cmd);
        r.ShouldHaveValidationErrorFor(c => c.AttemptIds);
    }

    [Fact]
    public async Task ContainsDuplicates_Fails()
    {
        var dup = Guid.NewGuid();
        var cmd = new AdminBulkAcknowledgeWikidataCoverCommand(
            AttemptIds: new[] { dup, dup },
            Note: null,
            TriggeredByUserId: _userId);

        var r = await _sut.TestValidateAsync(cmd);
        r.ShouldHaveValidationErrorFor(c => c.AttemptIds);
    }

    [Fact]
    public async Task NoteOver500Chars_Fails()
    {
        var cmd = new AdminBulkAcknowledgeWikidataCoverCommand(
            AttemptIds: new[] { Guid.NewGuid() },
            Note: new string('a', AdminBulkAcknowledgeWikidataCoverCommandValidator.MaxNoteLength + 1),
            TriggeredByUserId: _userId);

        var r = await _sut.TestValidateAsync(cmd);
        r.ShouldHaveValidationErrorFor(c => c.Note)
            .WithErrorMessage($"Note must be {AdminBulkAcknowledgeWikidataCoverCommandValidator.MaxNoteLength} characters or fewer.");
    }

    [Fact]
    public async Task TriggeredByUserIdEmpty_Fails()
    {
        var cmd = new AdminBulkAcknowledgeWikidataCoverCommand(
            AttemptIds: new[] { Guid.NewGuid() },
            Note: null,
            TriggeredByUserId: Guid.Empty);

        var r = await _sut.TestValidateAsync(cmd);
        r.ShouldHaveValidationErrorFor(c => c.TriggeredByUserId);
    }

    [Fact]
    public async Task ValidCommand_NoteNull_Passes()
    {
        var cmd = new AdminBulkAcknowledgeWikidataCoverCommand(
            AttemptIds: new[] { Guid.NewGuid(), Guid.NewGuid() },
            Note: null,
            TriggeredByUserId: _userId);

        var r = await _sut.TestValidateAsync(cmd);
        r.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task ValidCommand_NoteAtMaxLength_Passes()
    {
        var cmd = new AdminBulkAcknowledgeWikidataCoverCommand(
            AttemptIds: new[] { Guid.NewGuid() },
            Note: new string('a', AdminBulkAcknowledgeWikidataCoverCommandValidator.MaxNoteLength),
            TriggeredByUserId: _userId);

        var r = await _sut.TestValidateAsync(cmd);
        r.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void MaxBatchSize_Is50()
    {
        // ADR-driven cap; mirror of F2 bulk-retry budget. If this changes the
        // operations runbook + FE multi-select limit MUST update too.
        AdminBulkAcknowledgeWikidataCoverCommandValidator.MaxBatchSize
            .Should().Be(50, "DEC-3e rate-limiter budget caps each bulk click at 50 acknowledgements");
    }

    [Fact]
    public void MaxNoteLength_Is500()
    {
        // DEC-F-4: free-text note is log-only, capped at 500 to keep payload small.
        AdminBulkAcknowledgeWikidataCoverCommandValidator.MaxNoteLength
            .Should().Be(500, "DEC-F-4 caps acknowledgement notes at 500 chars (log-only)");
    }

    [Fact]
    public void OutcomeConstants_Defined()
    {
        // F5 contract: 4 outcome strings must exist for the result row payload.
        AdminBulkAcknowledgeRow.OutcomeAcked.Should().Be("acked");
        AdminBulkAcknowledgeRow.OutcomeAlreadyAcked.Should().Be("already-acked");
        AdminBulkAcknowledgeRow.OutcomeNotFound.Should().Be("not-found");
        AdminBulkAcknowledgeRow.OutcomeWrongState.Should().Be("wrong-state");
    }
}
