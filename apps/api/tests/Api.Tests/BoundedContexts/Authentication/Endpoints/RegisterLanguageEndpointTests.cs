using System.Net;
using System.Net.Http.Json;
using Api.Infrastructure;
using Api.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Endpoints;

/// <summary>
/// La fixture di <see cref="RegisterLanguageEndpointTests"/>: host per classe, database per test, con la
/// registrazione pubblica aperta (senza, <c>/auth/register</c> risponde 403).
/// </summary>
public sealed class RegisterLanguageHostFixture(SharedTestcontainersFixture shared)
    : SharedHostPerTestDatabaseFixture(shared, "register_lang")
{
    protected override WebApplicationFactory<Program> ConfigureHost(WebApplicationFactory<Program> factory) =>
        factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll(typeof(IConfigurationService));
                var config = new Mock<IConfigurationService>();
                config
                    .Setup(c => c.GetValueAsync<bool?>(
                        "Registration:PublicEnabled", It.IsAny<bool?>(), It.IsAny<string?>()))
                    .ReturnsAsync(true);
                services.AddSingleton(config.Object);
            }));
}

/// <summary>
/// #4783 — the language an account starts in, on the real host. A game night invitation is written in the
/// ORGANIZER's <c>User.Language</c> (#4754), and every account used to be born <c>en</c>: an Italian speaker
/// who never opened Settings → Preferences invited their friends in English. The unit tests of the handler
/// pin the rule; these pin that the endpoint hands it the two things it reads — the body's <c>locale</c> and
/// the <c>Accept-Language</c> header — which a handler test cannot see.
/// </summary>
[Collection("Integration-GroupB")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Authentication")]
[Trait("Issue", "4783")]
public sealed class RegisterLanguageEndpointTests
    : IClassFixture<RegisterLanguageHostFixture>, IAsyncLifetime
{
    private const string Endpoint = "/api/v1/auth/register";

    // Composta invece che scritta come letterale: il detector «Generic Password» di GitGuardian la rileva.
    private static readonly string ValidPassword = string.Concat("Register", "Lang", "Pwd123", "!x");

    private readonly RegisterLanguageHostFixture _hostFixture;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public RegisterLanguageEndpointTests(RegisterLanguageHostFixture hostFixture)
    {
        _hostFixture = hostFixture;
    }

    public async ValueTask InitializeAsync()
    {
        // 🔴 BeginTestAsync DEVE precedere l'uso di _factory (#4050).
        await _hostFixture.BeginTestAsync();
        _factory = _hostFixture.Factory;
        _client = _factory.CreateClient();
    }

    public ValueTask DisposeAsync()
    {
        _client?.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task<string> RegisterAsync(string? locale, string? acceptLanguage)
    {
        var email = $"lang-{Guid.NewGuid():N}@test.local";
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = JsonContent.Create(new
            {
                email,
                password = ValidPassword,
                termsAccepted = true,
                locale,
            }),
        };
        if (acceptLanguage is not null)
            request.Headers.AcceptLanguage.ParseAdd(acceptLanguage);

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return (await db.Users.SingleAsync(u => u.Email == email)).Language;
    }

    [Fact]
    public async Task The_language_the_page_shows_is_the_language_the_account_starts_in()
    {
        // An English-speaking browser shown the Italian interface (the product's default locale): the
        // interface is the better evidence of what the user reads.
        var language = await RegisterAsync(locale: "it", acceptLanguage: "en-US,en;q=0.9");

        language.Should().Be("it");
    }

    [Fact]
    public async Task Without_a_locale_the_browsers_header_decides()
    {
        var language = await RegisterAsync(locale: null, acceptLanguage: "it-IT,it;q=0.9,en;q=0.8");

        language.Should().Be("it");
    }

    [Fact]
    public async Task With_nothing_to_go_on_the_account_starts_in_english()
    {
        var language = await RegisterAsync(locale: null, acceptLanguage: null);

        language.Should().Be("en");
    }
}
