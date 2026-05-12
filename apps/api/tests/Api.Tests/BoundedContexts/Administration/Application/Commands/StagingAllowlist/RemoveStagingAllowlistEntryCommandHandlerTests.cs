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
public class RemoveStagingAllowlistEntryCommandHandlerTests
{
    private readonly Mock<IStagingAllowlistRepository> _repository = new(MockBehavior.Strict);
    private readonly Mock<IAuditLogRepository> _auditLog = new(MockBehavior.Strict);
    private readonly Mock<IUnitOfWork> _uow = new(MockBehavior.Strict);

    private RemoveStagingAllowlistEntryCommandHandler CreateHandler()
        => new(_repository.Object, _auditLog.Object, _uow.Object);

    [Fact]
    public async Task Handle_ExistingEntry_SoftDeletesAndAuditLog()
    {
        var entry = StagingAllowlistEntry.Create("user@example.com", addedByUserId: null, note: null);
        entry.ClearDomainEvents();

        _repository.Setup(r => r.GetByIdAsync(entry.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);
        _repository.Setup(r => r.UpdateAsync(entry, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _auditLog.Setup(a => a.AddAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(2);

        var removerId = Guid.NewGuid();
        await CreateHandler().Handle(
            new RemoveStagingAllowlistEntryCommand(entry.Id, removerId),
            CancellationToken.None);

        entry.IsDeleted.Should().BeTrue();
        entry.DeletedByUserId.Should().Be(removerId);

        _auditLog.Verify(a => a.AddAsync(
            It.Is<AuditLog>(log =>
                log.Action == "staging_allowlist.removed"
                && log.Resource == "staging_allowlist"
                && log.ResourceId == "user@example.com"
                && log.UserId == removerId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_MissingEntry_ThrowsNotFound()
    {
        var id = Guid.NewGuid();
        _repository.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((StagingAllowlistEntry?)null);

        var act = () => CreateHandler().Handle(
            new RemoveStagingAllowlistEntryCommand(id, Guid.NewGuid()),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"*{id}*not found*");

        _repository.Verify(r => r.UpdateAsync(It.IsAny<StagingAllowlistEntry>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
