using System.Net;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure;

/// <summary>
/// #4585 — i comandi admin che salvano un gioco non devono toccare le colonne di <c>shared_games</c> che
/// non cambiano, via HTTP e verificate sul database.
/// </summary>
/// <remarks>
/// <para>
/// <c>SharedGameRepository.Update</c> segnava come modificata ogni colonna, e <c>MapToEntity</c> non
/// ne copiava quattro: <c>has_knowledge_base</c>, <c>is_rag_public</c>, <c>bgg_raw_data</c> e
/// <c>test_run_id</c> arrivavano al database al default. Il gioco usciva dal filtro «AI-ready» e perdeva
/// la regola RAG pubblica a ogni invio in approvazione, a ogni FAQ aggiunta, a ogni modifica.
/// </para>
/// <para>
/// <c>PUT …/rag-access</c> era l'altra faccia: l'aggregato passava a <c>true</c>, ma la colonna non era
/// nel mapper e la riga restava a <c>false</c>, con un 204.
/// </para>
/// <para>
/// Il test unit <c>SharedGameRepositoryUnownedColumnsTests</c> copre tutte le colonne su InMemory, guidato
/// dal modello EF; qui si prova l'SQL vero. Classe parziale per riusare l'host della classe (#4050).
/// </para>
/// </remarks>
public sealed partial class SharedGameCatalogEndpointsIntegrationTests
{
    [Fact]
    public async Task SubmitForApproval_KeepsTheColumnsThatAreNotChanged()
    {
        var token = await CreateAdminTokenAsync();
        var game = await CreateGameWithColumnsTheAggregateDoesNotOwnAsync(status: GameStatus.Draft);

        using var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"/api/v1/admin/shared-games/{game.Id}/submit-for-approval",
            token);
        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent, body);

        var after = await GameRowAsync(game.Id);
        after.Status.Should().Be((int)GameStatus.PendingApproval, "il comando ha fatto il suo lavoro");
        AssertTheUnchangedColumnsAreKept(game, after);
    }

    [Fact]
    public async Task AddFaq_KeepsTheColumnsThatAreNotChanged()
    {
        var token = await CreateAdminTokenAsync();
        var game = await CreateGameWithColumnsTheAggregateDoesNotOwnAsync();

        using var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"/api/v1/admin/shared-games/{game.Id}/faq",
            token,
            new { question = "Si può commerciare col banco?", answer = "Sì, 4 a 1.", order = 1 });
        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created, body);

        (await FaqRowsAsync(game.Id)).Should().ContainSingle("il comando ha fatto il suo lavoro");
        AssertTheUnchangedColumnsAreKept(game, await GameRowAsync(game.Id));
    }

    /// <summary>
    /// <c>true</c> non arrivava mai alla riga; <c>false</c> la lasciava a <c>false</c> per caso, perché la
    /// colonna non era nel mapper. Parte da una riga con il valore opposto a quello impostato.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SetRagPublicAccess_WritesTheColumn_AndKeepsTheOthers(bool isRagPublic)
    {
        var token = await CreateAdminTokenAsync();
        var game = await CreateGameWithColumnsTheAggregateDoesNotOwnAsync(isRagPublic: !isRagPublic);

        using var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Put,
            $"/api/v1/admin/shared-games/{game.Id}/rag-access",
            token,
            new { isRagPublic });
        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent, body);

        var after = await GameRowAsync(game.Id);
        after.IsRagPublic.Should().Be(isRagPublic, "il 204 deve corrispondere a una riga cambiata");
        after.HasKnowledgeBase.Should().Be(game.HasKnowledgeBase);
        after.BggRawData.Should().Be(game.BggRawData);
        after.TestRunId.Should().Be(game.TestRunId);
    }

    /// <summary>
    /// Le quattro colonne del difetto, com'erano prima del comando: nessun comando di questi test le cambia.
    /// </summary>
    private static void AssertTheUnchangedColumnsAreKept(SharedGameEntity before, SharedGameEntity after)
    {
        after.HasKnowledgeBase.Should().Be(before.HasKnowledgeBase,
            "azzerata, il gioco esce dal filtro «AI-ready» del catalogo");
        after.IsRagPublic.Should().Be(before.IsRagPublic, "nessun job la ripara: la regola RAG pubblica sparisce");
        after.BggRawData.Should().Be(before.BggRawData, "serve a ripopolare il gioco da BGG");
        after.TestRunId.Should().Be(before.TestRunId, "senza, il gioco di test non viene più raccolto dalla pulizia");
        after.Title.Should().Be(before.Title);
        after.Description.Should().Be(before.Description);
    }
}
