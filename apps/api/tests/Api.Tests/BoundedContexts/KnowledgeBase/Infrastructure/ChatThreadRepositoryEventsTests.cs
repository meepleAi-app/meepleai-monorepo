using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.Interfaces;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure;

/// <summary>
/// #4552: <see cref="ChatThreadRepository"/> ricostruisce il thread con il costruttore pubblico, che solleva
/// <c>ChatThreadCreatedEvent</c>, e ricarica ogni messaggio con <see cref="ChatThread.AddMessage"/>, che solleva un
/// <c>MessageAddedEvent</c> per messaggio. Solo <c>GetByLiveSessionIdAsync</c> puliva gli eventi (#4276). Da ogni
/// altra lettura, il primo <c>UpdateAsync</c> (un messaggio nuovo, una chiusura, una cancellazione) li raccoglieva
/// tutti: l'outbox e l'audit ricevevano di nuovo la creazione del thread e l'intera storia della conversazione a
/// ogni turno. Trovato dal gate <c>RepositoryReconstitutionArchitectureTests</c> esteso ai metodi d'istanza.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4552")]
public sealed class ChatThreadRepositoryEventsTests : IDisposable
{
    private readonly MeepleAiDbContext _db;

    public ChatThreadRepositoryEventsTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"ChatThreadRepoEvents_{Guid.NewGuid()}")
            .Options;
        _db = new MeepleAiDbContext(
            options,
            new Mock<IMediator>().Object,
            new Mock<IDomainEventCollector>().Object);
    }

    public void Dispose() => _db.Dispose();

    private async Task<Guid> SeedThreadWithTwoMessagesAsync()
    {
        var thread = new ChatThread(Guid.NewGuid(), Guid.NewGuid(), title: "partita");
        thread.AddMessage(new ChatMessage("come si muove la torre?", ChatMessage.UserRole, 1, DateTime.UtcNow.AddMinutes(-5)));
        thread.AddMessage(new ChatMessage("in orizzontale e in verticale", ChatMessage.AssistantRole, 2, DateTime.UtcNow.AddMinutes(-4)));

        await new ChatThreadRepository(_db, new Mock<IDomainEventCollector>().Object).AddAsync(thread);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        return thread.Id;
    }

    [Fact]
    public async Task GetByIdAsync_RaisesNoDomainEvent()
    {
        var id = await SeedThreadWithTwoMessagesAsync();

        var thread = await new ChatThreadRepository(_db, new Mock<IDomainEventCollector>().Object).GetByIdAsync(id);

        thread!.Messages.Should().HaveCount(2);
        thread.DomainEvents.Select(e => e.GetType().Name).Should().BeEmpty(
            "reading a stored thread is not creating it nor adding its messages again");
    }

    [Fact]
    public async Task UpdateAsync_AfterARead_CollectsOnlyTheEventsOfTheChange()
    {
        var id = await SeedThreadWithTwoMessagesAsync();
        var collected = new List<string>();
        var collector = new Mock<IDomainEventCollector>();
        collector
            .Setup(c => c.CollectEventsFrom(It.IsAny<IAggregateRoot>()))
            .Callback<IAggregateRoot>(a => collected.AddRange(a.DomainEvents.Select(e => e.GetType().Name)));

        var repository = new ChatThreadRepository(_db, collector.Object);
        var thread = await repository.GetByIdAsync(id);
        thread!.AddMessage(new ChatMessage("e l'alfiere?", ChatMessage.UserRole, 3));
        await repository.UpdateAsync(thread);

        // Il repository passa al collettore ciò che l'aggregato ha sollevato, e il salvataggio lo scrive nella
        // outbox e nell'audit: la creazione del thread e i due messaggi vecchi non devono ripartire.
        collected.Should().Equal("MessageAddedEvent");
    }
}
