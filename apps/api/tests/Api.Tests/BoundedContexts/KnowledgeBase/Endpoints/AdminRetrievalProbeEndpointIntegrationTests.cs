using System.Net;
using System.Net.Http.Json;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.ProbeRetrieval;
using Api.BoundedContexts.KnowledgeBase.Application.Services.Answering;
using Api.Infrastructure;
using Api.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Endpoints;

/// <summary>
/// Host e database una volta per classe (#4050). Condividerli è sicuro qui: i test non scrivono altro
/// che utenti e sessioni (find-or-create sull'id, id statici), e nessuno legge un conteggio o una lista
/// globale — la sonda chiede di un gioco che nessuno semina.
/// </summary>
public sealed class AdminRetrievalProbeHostFixture(SharedTestcontainersFixture shared)
    : IntegrationHostFixture(shared, "kb_retrieval_probe");

/// <summary>
/// #4482: <c>POST /api/v1/admin/kb/retrieval-probe</c> sta dietro la sessione admin, e l'host la sa
/// comporre: lettore del profilo, rilevatore della lingua e recupero arrivano dal container vero.
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4482")]
public sealed class AdminRetrievalProbeEndpointIntegrationTests
    : IClassFixture<AdminRetrievalProbeHostFixture>, IAsyncLifetime
{
    private const string Route = "/api/v1/admin/kb/retrieval-probe";

    private static readonly Guid AdminId = Guid.Parse("00000000-0000-0000-0000-0000000448a1");
    private static readonly Guid UserId = Guid.Parse("00000000-0000-0000-0000-0000000448b1");

    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private string _adminToken = null!;
    private string _userToken = null!;

    public AdminRetrievalProbeEndpointIntegrationTests(AdminRetrievalProbeHostFixture host)
    {
        _factory = host.Factory;
        _client = host.Client;
    }

    public async ValueTask InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        (_, _adminToken) = await TestSessionHelper.CreateAdminSessionAsync(db, AdminId);
        (_, _userToken) = await TestSessionHelper.CreateUserSessionAsync(db, UserId);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private const string Question = "Posso regalare una risorsa?";

    private static object Body => new { gameId = Guid.NewGuid(), question = Question };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Probe_WithoutSession_Returns401()
    {
        var response = await _client.PostAsJsonAsync(Route, Body, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Probe_WithAUserSession_Returns403()
    {
        var response = await _client.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(HttpMethod.Post, Route, _userToken, Body), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Probe_WithAnAdminSession_OnAGameWithoutDocuments_Returns200_WithThePublishedProfileAndNoChunks()
    {
        var response = await _client.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(HttpMethod.Post, Route, _adminToken, Body), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        var probe = await response.Content.ReadFromJsonAsync<RetrievalProbeDto>(Ct);

        // La versione e la lingua sono quelle della chat: lo stesso lettore e lo stesso rilevatore, dallo
        // stesso container. La lingua non è scritta qui: la decide il rilevatore, che ha una sua issue (#4480).
        using var scope = _factory.Services.CreateScope();
        var published = await scope.ServiceProvider.GetRequiredService<IPublishedAgentProfileReader>().GetAsync(Ct);
        var expectedLanguage = AnswerLanguageResolver.Resolve(
            scope.ServiceProvider.GetRequiredService<ILanguageDetectionService>(), Question, "it");

        probe.Should().NotBeNull();
        probe!.ProfileVersionId.Should().Be(published.VersionId);
        probe.Parameters.CandidatePoolSize.Should().Be(published.Content.CandidatePoolSize);
        probe.Parameters.FinalTopK.Should().Be(published.Content.FinalTopK);
        probe.AcceptLanguage.Should().Be("it");
        probe.Language.Should().Be(expectedLanguage.Language);
        probe.LanguageSource.Should().Be(expectedLanguage.Source.ToString());
        probe.Pool.Should().BeEmpty();
        probe.Final.Should().BeEmpty();
    }
}
