using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.Validators;
using Api.SharedKernel.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Validators;

/// <summary>
/// #3842 — <c>PUT /admin/users/{id}/tier</c> rifiutava <b>ogni</b> valore.
///
/// <para>
/// Il validatore aveva un elenco proprio — <c>{ Free, Basic, Pro, Enterprise }</c>, confrontato in
/// modo case-sensitive — sbagliato tre volte: conteneva <c>Basic</c>, che il dominio non conosce;
/// ometteva <c>normal</c> e <c>premium</c>, che invece riconosce; e rifiutava le minuscole, cioe'
/// la forma in cui i tier sono scritti nel database (<c>free</c>, <c>premium</c>).
/// </para>
/// <para>
/// Il test non elenca i tier a mano: li prende da <c>UserTier.All</c>. Un test con la propria copia
/// dell'elenco sarebbe la quarta, e replicherebbe il difetto che dovrebbe impedire.
/// </para>
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
[Trait("Issue", "3842")]
public class UpdateUserTierCommandValidatorTests
{
    private readonly UpdateUserTierCommandValidator _validator = new();

    public static TheoryData<string> TierRiconosciuti()
    {
        var dati = new TheoryData<string>();
        foreach (var tier in UserTier.All)
        {
            dati.Add(tier);
        }
        return dati;
    }

    [Theory]
    [MemberData(nameof(TierRiconosciuti))]
    public void Validate_AccettaOgniTierCheIlDominioRiconosce(string tier)
    {
        var comando = new UpdateUserTierCommand(Guid.NewGuid(), tier, Guid.NewGuid());

        var esito = _validator.Validate(comando);

        esito.IsValid.Should().BeTrue(
            $"'{tier}' e' un tier valido per UserTier: un validatore piu' severo del dominio " +
            "rende l'endpoint inutilizzabile (#3842)");
    }

    [Theory]
    [InlineData("FREE")]
    [InlineData("Premium")]
    [InlineData("pRo")]
    public void Validate_NonDistingueMaiuscoleEMinuscole(string tier)
    {
        // I tier nel database sono minuscoli; l'interfaccia puo' mandarli capitalizzati.
        // Il confronto ordinale del vecchio elenco faceva cadere entrambe le forme.
        var comando = new UpdateUserTierCommand(Guid.NewGuid(), tier, Guid.NewGuid());

        _validator.Validate(comando).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("Basic")]      // stava nell'elenco vecchio, il dominio non lo conosce
    [InlineData("gold")]
    [InlineData("")]
    public void Validate_RifiutaCioCheIlDominioNonRiconosce(string tier)
    {
        var comando = new UpdateUserTierCommand(Guid.NewGuid(), tier, Guid.NewGuid());

        var esito = _validator.Validate(comando);

        esito.IsValid.Should().BeFalse(
            "allineare il validatore al dominio non deve renderlo permissivo");
    }
}
