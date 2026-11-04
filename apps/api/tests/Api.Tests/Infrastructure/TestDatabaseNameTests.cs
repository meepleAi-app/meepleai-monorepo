using System;
using System.Linq;
using System.Reflection;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Infrastructure;

/// <summary>
/// Issue #4050 — il limite dei 63 byte sui nomi di database generati dalle fixture.
/// </summary>
/// <remarks>
/// Il difetto da cui nascono questi test: un prefisso da 26 caratteri faceva uscire il nome del
/// template a 64 byte, Postgres lo troncava a 63, e la <c>CREATE DATABASE … TEMPLATE</c>
/// continuava a funzionare. Smetteva di funzionare solo la query che cerca quel database per
/// <b>nome</b> in <c>pg_stat_activity</c> — un confronto fra stringhe, non fra identificatori —
/// quindi i backend del template non venivano mai terminati e ogni clone moriva su <c>55006</c>.
/// Tre test persi e una diagnostica che accusava una coda di chiusura inesistente.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
public sealed class TestDatabaseNameTests
{
    [Theory]
    [InlineData(5)]   // SharedHostPerTestDatabaseFixture: _tmpl, _boot, _tNNN
    [InlineData(0)]   // IntegrationHostFixture: nessun suffisso
    public void APrefixAtTheBudgetProducesANameThatFitsInSixtyThreeBytes(int suffixLength)
    {
        var budget = TestDatabaseName.MaxIdentifierLength - TestDatabaseName.GuidPartLength - suffixLength;
        var prefix = new string('a', budget);

        TestDatabaseName.ValidatePrefix(prefix, suffixLength, "p");

        // La composizione che le fixture fanno davvero: prefisso + _ + 32 esadecimali + suffisso.
        var longestName = $"{prefix}_{Guid.NewGuid():N}{new string('x', suffixLength)}";
        longestName.Length.Should().BeLessThanOrEqualTo(
            TestDatabaseName.MaxIdentifierLength,
            "il budget esiste perche' un nome piu' lungo viene troncato da Postgres in silenzio");
    }

    [Theory]
    [InlineData(5)]
    [InlineData(0)]
    public void APrefixOneCharacterOverTheBudgetIsRefused(int suffixLength)
    {
        var budget = TestDatabaseName.MaxIdentifierLength - TestDatabaseName.GuidPartLength - suffixLength;
        var prefix = new string('a', budget + 1);

        var act = () => TestDatabaseName.ValidatePrefix(prefix, suffixLength, "p");

        act.Should().Throw<ArgumentException>()
            .WithMessage("*63*", "il messaggio deve dire da dove viene il limite")
            .And.ParamName.Should().Be("p");
    }

    /// <summary>
    /// Il caso reale che ha prodotto il difetto, con il nome vero e il conto vero.
    /// </summary>
    [Fact]
    public void TheTwentySixCharacterPrefixThatBrokeTheCloneIsRefused()
    {
        const string theOffender = "wizard_superadmin_approval";   // 26 caratteri
        theOffender.Length.Should().Be(26, "e' il prefisso che in #4050 ha fatto fallire tre test");

        var act = () => TestDatabaseName.ValidatePrefix(theOffender, longestSuffixLength: 5, "p");

        act.Should().Throw<ArgumentException>(
            "26 + 1 + 32 + 5 = 64 byte: un carattere oltre il limite, e Postgres non lo dice");
    }

    [Theory]
    [InlineData("Has_Uppercase")]
    [InlineData("has-hyphen")]
    [InlineData("has space")]
    [InlineData("has\"quote")]
    public void APrefixWithCharactersOutsideTheAllowedSetIsRefused(string prefix)
    {
        var act = () => TestDatabaseName.ValidatePrefix(prefix, longestSuffixLength: 5, "p");

        act.Should().Throw<ArgumentException>(
            "il prefisso finisce in una CREATE DATABASE interpolata");
    }

    /// <summary>
    /// 🔴 La parte che conta: ogni prefisso effettivamente usato nel repo passa la validazione.
    /// </summary>
    /// <remarks>
    /// Senza questo, la validazione protegge solo i prefissi futuri — e quelli esistenti restano
    /// a fallire a runtime, dentro l'<c>InitializeAsync</c> di una fixture, cioe' nel punto in cui
    /// l'errore si presenta come «tutti i test di questa classe sono rossi».
    /// </remarks>
    [Fact]
    public void EveryPrefixUsedInTheRepoPassesItsOwnFixtureBudget()
    {
        var fixtures = typeof(TestDatabaseNameTests).Assembly.GetTypes()
            .Where(t => !t.IsAbstract)
            .Where(t => typeof(SharedHostPerTestDatabaseFixture).IsAssignableFrom(t)
                     || typeof(IntegrationHostFixture).IsAssignableFrom(t))
            .ToList();

        // 🔴 Senza questo, il test «passerebbe» anche se la riflessione non trovasse piu' nessuna
        // fixture — per una rinomina della base, per uno spostamento di namespace — ed e' il modo
        // tipico in cui un gate smette di controllare qualcosa senza diventare rosso.
        fixtures.Should().NotBeEmpty(
            "la riflessione deve trovare le fixture concrete: zero significa che questo test non " +
            "sta controllando nulla, non che va tutto bene");

        var refused = new List<string>();
        var unconstructible = new List<string>();

        foreach (var type in fixtures)
        {
            switch (TryConstruct(type))
            {
                case { Refusal: { } refusal }:
                    refused.Add($"{type.Name}: {refusal}");
                    break;
                case { Other: { } other }:
                    unconstructible.Add($"{type.Name}: {other}");
                    break;
            }
        }

        refused.Should().BeEmpty(
            "una fixture il cui prefisso sfora non si manifesta come errore di compilazione: " +
            "esplode nell'InitializeAsync della fixture, e ogni test della sua classe risulta rosso");

        // Un guasto diverso dalla validazione non e' un difetto di prefisso, ma tacerlo
        // significherebbe non aver controllato quella fixture pretendendo di averlo fatto.
        unconstructible.Should().BeEmpty(
            "ogni fixture deve essere costruibile con un solo SharedTestcontainersFixture: se la " +
            "forma del costruttore cambia, questo test smette di coprirla e deve dirlo");
    }

    /// <summary>
    /// Costruisce la fixture con uno <c>SharedTestcontainersFixture</c> non inizializzato: il
    /// costruttore della base valida il prefisso e non tocca Docker (quella classe non ha un
    /// costruttore proprio, solo inizializzatori di campo), quindi basta a esercitare la
    /// validazione senza avviare un container.
    /// </summary>
    private static (string? Refusal, string? Other) TryConstruct(Type fixtureType)
    {
        try
        {
            Activator.CreateInstance(fixtureType, new SharedTestcontainersFixture());
            return (null, null);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is ArgumentException inner)
        {
            return (inner.Message, null);
        }
        catch (Exception ex)
        {
            return (null, $"{ex.GetType().Name}: {ex.Message}");
        }
    }
}
