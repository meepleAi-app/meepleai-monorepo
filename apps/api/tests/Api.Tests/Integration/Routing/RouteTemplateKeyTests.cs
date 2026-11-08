using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.AspNetCore.Routing.Patterns;
using Xunit;

namespace Api.Tests.Integration.Routing;

/// <summary>
/// #4579 — la chiave con cui <see cref="NoDuplicateRouteTests"/> decide che due rotte sono la stessa: nome del
/// parametro tolto, resto tenuto.
/// </summary>
/// <remarks>
/// Funzione pura, quindi senza fixture né host: nella classe della guardia ogni riga avrebbe pagato la
/// clonazione di un database. Qui si prova cosa fa la <b>chiave</b>, non come si comporta il routing: che ASP.NET
/// consideri o no ambigui due modelli con la stessa chiave, o con chiavi diverse, non è oggetto di questi test.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
[Trait("Issue", "4579")]
public sealed class RouteTemplateKeyTests
{
    [Theory]
    // Il caso di #4579: stesso percorso, stesso vincolo, nome diverso.
    [InlineData("api/v1/admin/shared-games/{gameId:guid}/documents/overview", "api/v1/admin/shared-games/{sharedGameId:guid}/documents/overview", true)]
    [InlineData("api/v1/games/{id}", "api/v1/games/{slug}", true)]
    [InlineData("files/{*path}", "files/{*rest}", true)]
    // Il vincolo, il catch-all e il percorso fanno parte della chiave.
    [InlineData("api/v1/games/{id:guid}", "api/v1/games/{id}", false)]
    [InlineData("api/v1/games/{id:guid}", "api/v1/games/{id:int}", false)]
    [InlineData("files/{path}", "files/{*path}", false)]
    [InlineData("api/v1/games/{id:guid}/errata", "api/v1/games/{id:guid}/faq", false)]
    [InlineData("api/v1/games/{id:guid}", "api/v1/games/{id:guid}/errata", false)]
    // L'opzionalità fa parte della chiave: la guardia non segnala {id} e {id?} come duplicati. Non è verificato se
    // il routing li consideri ambigui; se lo fossero, la guardia non li vedrebbe. Oggi non conta: nei modelli di
    // rotta di apps/api/src/Api non c'è nessun parametro opzionale
    // (git grep -E '\{\*?[A-Za-z_]+(:[^}"]*)?\?\}' -- '*.cs' non trova righe).
    [InlineData("api/v1/games/{id}", "api/v1/games/{id?}", false)]
    public void TemplateKey_IgnoresTheParameterNameAndKeepsTheRest(string left, string right, bool sameKey)
    {
        var leftKey = NoDuplicateRouteTests.TemplateWithoutParameterNames(RoutePatternFactory.Parse(left));
        var rightKey = NoDuplicateRouteTests.TemplateWithoutParameterNames(RoutePatternFactory.Parse(right));

        (leftKey == rightKey).Should().Be(sameKey, $"'{left}' → '{leftKey}', '{right}' → '{rightKey}'");
    }
}
