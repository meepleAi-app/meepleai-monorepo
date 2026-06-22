using Api.BoundedContexts.Administration.Application.Commands.StagingAllowlist;
using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Commands.StagingAllowlist;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
public class AddStagingAllowlistEntryCommandHandlerTests
{
    private readonly Mock<IStagingAllowlistRepository> _repository = new(MockBehavior.Strict);
    private readonly Mock<IAuditLogRepository> _auditLog = new(MockBehavior.Strict);
    private readonly Mock<IUnitOfWork> _uow = new(MockBehavior.Strict);

    private AddStagingAllowlistEntryCommandHandler CreateHandler()
        => new(_repository.Object, _auditLog.Object, _uow.Object);

    [Fact]
    public async Task Handle_NewEmail_NormalizesAddsEntryAndAuditLog()
    {
        _repository.Setup(r => r.ExistsByEmailAsync("badsworm@gmail.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _repository.Setup(r => r.AddAsync(It.IsAny<StagingAllowlistEntry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _auditLog.Setup(a => a.AddAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(2);

        var userId = Guid.NewGuid();
        var command = new AddStagingAllowlistEntryCommand("  BADsworm@Gmail.com  ", "test note", userId);

        var dto = await CreateHandler().Handle(command, CancellationToken.None);

        dto.Email.Should().Be("badsworm@gmail.com");
        dto.AddedByUserId.Should().Be(userId);
        dto.Note.Should().Be("test note");

        _auditLog.Verify(a => a.AddAsync(
            It.Is<AuditLog>(log =>
                log.Action == "staging_allowlist.added"
                && log.Resource == "staging_allowlist"
                && log.ResourceId == "badsworm@gmail.com"
                && log.Result == "success"
                && log.UserId == userId),
            It.IsAny<CancellationToken>()), Times.Once);

        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DuplicateEmail_ThrowsConflict()
    {
        _repository.Setup(r => r.ExistsByEmailAsync("dup@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var act = () => CreateHandler().Handle(
            new AddStagingAllowlistEntryCommand("dup@example.com", null, Guid.NewGuid()),
            CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*dup@example.com*already*");

        _repository.Verify(r => r.AddAsync(It.IsAny<StagingAllowlistEntry>(), It.IsAny<CancellationToken>()), Times.Never);
        _auditLog.VerifyNoOtherCalls();
        _uow.VerifyNoOtherCalls();
    }
}
