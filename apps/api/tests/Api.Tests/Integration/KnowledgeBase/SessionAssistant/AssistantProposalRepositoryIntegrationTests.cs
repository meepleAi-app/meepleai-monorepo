using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.KnowledgeBase.SessionAssistant;

/// <summary>
/// Host e database una volta per classe: ogni test usa una sessione live con un id nuovo e legge solo le
/// proprie proposte, quindi condividere il database è sicuro.
/// </summary>
public sealed class AssistantProposalRepositoryHostFixture(SharedTestcontainersFixture shared)
    : IntegrationHostFixture(shared, "assistant_proposals");

/// <summary>
/// Spec 2026-10-11 §5.2 e §5.3, sul database vero: la proposta sopravvive al salvataggio con le sue tre
/// colonne jsonb, e la prenotazione della conferma perde sulla concorrenza quando arriva seconda.
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4275")]
public sealed class AssistantProposalRepositoryIntegrationTests(AssistantProposalRepositoryHostFixture host)
    : IClassFixture<AssistantProposalRepositoryHostFixture>
{
    private static readonly Guid Marco = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid HostUser = Guid.Parse("00000000-0000-0000-0000-0000000000b1");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static AssistantProposal NewScore(Guid session, Guid thread) =>
        AssistantProposal.ForScore(session, thread, Guid.NewGuid(), Guid.NewGuid(), Marco, "Marco", "punti", 2, 5, 7, DateTime.UtcNow);

    private async Task<Guid> SaveAsync(AssistantProposal proposal)
    {
        await using var scope = host.Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IAssistantProposalRepository>().AddAsync(proposal, Ct);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
        return proposal.Id;
    }

    [Fact]
    public async Task AProposal_SurvivesTheRoundTrip_WithItsJsonColumns()
    {
        var session = Guid.NewGuid();
        var id = await SaveAsync(NewScore(session, Guid.NewGuid()));

        await using var scope = host.Factory.Services.CreateAsyncScope();
        var loaded = await scope.ServiceProvider.GetRequiredService<IAssistantProposalRepository>().GetAsync(id, Ct);

        loaded.Should().NotBeNull();
        loaded!.Status.Should().Be(AssistantProposalStatus.Pending);
        loaded.Payload.Should().Be(new AssistantProposalPayload(Marco, "Marco", "punti", 2, 5));
        loaded.Expected.Should().Be(new AssistantProposalExpectation(7, null));
        loaded.ConfirmedPayload.Should().BeNull();
        loaded.LiveSessionId.Should().Be(session);
    }

    [Fact]
    public async Task AnEditedProposal_KeepsTheConfirmedPoints_AndLeavesTheOpenList()
    {
        var session = Guid.NewGuid();
        var id = await SaveAsync(NewScore(session, Guid.NewGuid()));
        await SaveAsync(NewScore(session, Guid.NewGuid()));

        await using (var scope = host.Factory.Services.CreateAsyncScope())
        {
            var proposal = await scope.ServiceProvider.GetRequiredService<IAssistantProposalRepository>().GetAsync(id, Ct);
            proposal!.BeginApplying(HostUser);
            proposal.MarkEdited(3, DateTime.UtcNow);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
        }

        await using var verify = host.Factory.Services.CreateAsyncScope();
        var repository = verify.ServiceProvider.GetRequiredService<IAssistantProposalRepository>();
        var edited = await repository.GetAsync(id, Ct);
        var open = await repository.ListOpenBySessionAsync(session, Ct);

        edited!.Status.Should().Be(AssistantProposalStatus.Edited);
        edited.ConfirmedPayload.Should().Be(new AssistantProposalConfirmation(3));
        edited.ResolvedBy.Should().Be(HostUser);
        open.Should().ContainSingle().Which.Id.Should().NotBe(id);
    }

    [Fact]
    public async Task SecondConcurrentSave_FailsOnXmin()
    {
        // La prenotazione della conferma (spec §5.3): due richieste caricano la stessa proposta Pending,
        // entrambe la portano ad Applying, e solo la prima che salva vince.
        var id = await SaveAsync(NewScore(Guid.NewGuid(), Guid.NewGuid()));

        await using var first = host.Factory.Services.CreateAsyncScope();
        await using var second = host.Factory.Services.CreateAsyncScope();
        var a = await first.ServiceProvider.GetRequiredService<IAssistantProposalRepository>().GetAsync(id, Ct);
        var b = await second.ServiceProvider.GetRequiredService<IAssistantProposalRepository>().GetAsync(id, Ct);
        a!.BeginApplying(HostUser);
        b!.BeginApplying(Guid.NewGuid());

        await first.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
        var act = () => second.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);

        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    [Fact]
    public async Task ListByThread_ReturnsEveryProposalOfTheThread_InOrder()
    {
        var thread = Guid.NewGuid();
        var session = Guid.NewGuid();
        var firstId = await SaveAsync(NewScore(session, thread));
        var secondId = await SaveAsync(AssistantProposal.ForAdvanceTurn(session, thread, Guid.NewGuid(), Guid.NewGuid(), 4, DateTime.UtcNow.AddSeconds(1)));

        await using var scope = host.Factory.Services.CreateAsyncScope();
        var proposals = await scope.ServiceProvider.GetRequiredService<IAssistantProposalRepository>().ListByThreadAsync(thread, Ct);

        proposals.Select(p => p.Id).Should().Equal(firstId, secondId);
    }
}
