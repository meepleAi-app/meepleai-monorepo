using Api.BoundedContexts.Administration.Application;
using Api.Infrastructure;
using Api.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Api.Tests.TestHelpers;

/// <summary>
/// Test double for <see cref="AuditService"/> whose <c>EnqueueAuditAtomicAsync</c> always throws: an
/// <c>[AtomicAudit]</c> command then fails after its handler, inside the open transaction, and rolls back.
/// The base constructor needs a <see cref="MeepleAiDbContext"/>; an InMemory one satisfies it, since its methods are
/// not invoked.
/// </summary>
internal sealed class ThrowingAuditService : AuditService
{
    public ThrowingAuditService()
        : base(
            new MeepleAiDbContext(
                new DbContextOptionsBuilder<MeepleAiDbContext>()
                    .UseInMemoryDatabase($"throwing_audit_{Guid.NewGuid()}")
                    .Options,
                Mock.Of<IMediator>(),
                Mock.Of<Api.SharedKernel.Application.Services.IDomainEventCollector>()),
            Mock.Of<Microsoft.Extensions.Logging.ILogger<AuditService>>(),
            timeProvider: null)
    {
    }

    public override Task EnqueueAuditAtomicAsync(
        AuditOutboxPayload payload,
        CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException(
            "Simulated audit write failure — verifies transaction rollback in atomic path.");
    }
}
