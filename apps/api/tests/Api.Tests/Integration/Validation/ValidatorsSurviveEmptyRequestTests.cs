using System.Reflection;
using Api.Tests.Constants;
using FluentAssertions;
using FluentValidation;
using Xunit;

namespace Api.Tests.Integration.Validation;

/// <summary>
/// #3847 — un validatore deve <b>segnalare</b> una richiesta malformata, non esplodere su di essa.
///
/// <para>
/// Trenta endpoint rispondevano 500 a un corpo vuoto (<c>{}</c>). La causa non era negli endpoint:
/// era nei validatori. Una regola come
/// </para>
/// <code>
/// RuleFor(x => x.Participants).Must(p => p.Count &lt;= 20);
/// </code>
/// <para>
/// dereferenzia <c>p</c> senza un <c>.NotNull()</c> davanti. Con il campo assente FluentValidation
/// non produce un errore di validazione: solleva <c>NullReferenceException</c>, che attraversa la
/// pipeline e diventa un 500. Per chi chiama, "ho dimenticato un campo" e "il server e' rotto"
/// diventano indistinguibili.
/// </para>
/// <para>
/// Questo test scorre <b>tutti</b> i validatori registrati nell'assembly e invoca ciascuno con
/// un'istanza in cui ogni riferimento e' null e ogni valore e' il default — cioe' esattamente il
/// corpo <c>{}</c> che l'audit ha usato per trovarli. Il test elenca i colpevoli invece di
/// limitarsi a dire che ce n'e' uno: la correzione e' un <c>.NotNull()</c> per volta, e serve
/// sapere dove.
/// </para>
/// <para>
/// <b>Se questo test fallisce su un validatore nuovo</b>: non aggiungerlo a un'allowlist. Metti
/// <c>.NotNull()</c> (o <c>.When(x =&gt; x.Campo != null)</c>) davanti alla regola che dereferenzia.
/// </para>
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedKernel")]
[Trait("Issue", "3847")]
public class ValidatorsSurviveEmptyRequestTests
{
    /// <summary>
    /// La protezione principale vive in una riga sola, dentro <c>AddFluentValidation()</c>. Questo
    /// controllo la rende visibile: toglierla farebbe fallire un test che dice perche', invece di
    /// far riemergere ventinove 500 mesi dopo.
    ///
    /// La configurazione e' applicata a tutta la suite da
    /// <see cref="Api.Tests.TestHelpers.FluentValidationTestConfiguration"/> — un ModuleInitializer,
    /// perche' <c>ValidatorOptions.Global</c> e' stato globale e l'ordine dei test non deve contare.
    /// </summary>
    [Fact]
    public void LaConfigurazioneDiProduzione_FermaLaCatenaAlPrimoErrore()
    {
        ValidatorOptions.Global.DefaultRuleLevelCascadeMode.Should().Be(CascadeMode.Stop,
            "con Continue una regola che dereferenzia gira anche dopo che NotEmpty ha fallito, " +
            "e la richiesta malformata torna come 500 invece che come errore di validazione (#3847)");
    }

    /// <summary>
    /// Istanza "tutto assente" del tipo validato: riferimenti a null, valori al default.
    /// Si evita il costruttore — che spesso valida gli argomenti — usando l'allocazione diretta.
    /// </summary>
    private static object? IstanzaVuota(Type tipo)
    {
        if (tipo.IsAbstract || tipo.IsInterface || tipo.ContainsGenericParameters)
        {
            return null;
        }

        try
        {
            return System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(tipo);
        }
        catch (Exception)
        {
            // Tipi che non si possono allocare cosi' (string, array, tipi con layout speciale):
            // non sono comandi, e non interessano questo controllo.
            return null;
        }
    }

    public static TheoryData<string> Validatori()
    {
        var dati = new TheoryData<string>();
        foreach (var t in TipiValidatore())
        {
            dati.Add(t.FullName!);
        }
        return dati;
    }

    private static IEnumerable<Type> TipiValidatore() =>
        typeof(Program).Assembly
            .GetTypes()
            .Where(t => t is { IsAbstract: false, IsGenericTypeDefinition: false })
            .Where(t => t.GetInterfaces().Any(i =>
                i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IValidator<>)))
            .Where(t => t.GetConstructor(Type.EmptyTypes) is not null)
            .OrderBy(t => t.FullName, StringComparer.Ordinal);

    [Theory]
    [MemberData(nameof(Validatori))]
    public void Validate_ConTuttiICampiAssenti_SegnalaInveceDiEsplodere(string nomeValidatore)
    {
        var tipoValidatore = TipiValidatore().Single(t => t.FullName == nomeValidatore);

        var interfaccia = tipoValidatore.GetInterfaces()
            .First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IValidator<>));
        var tipoValidato = interfaccia.GetGenericArguments()[0];

        var istanza = IstanzaVuota(tipoValidato);
        if (istanza is null)
        {
            return; // tipo non allocabile: non e' un comando/query, niente da provare
        }

        var validatore = (IValidator)Activator.CreateInstance(tipoValidatore)!;
        var contesto = new ValidationContext<object>(istanza);

        var azione = () => validatore.Validate(contesto);

        azione.Should().NotThrow(
            $"{tipoValidatore.Name} deve produrre errori di validazione su una richiesta vuota, " +
            "non un'eccezione: una regola che dereferenzia un campo assente diventa un 500 (#3847)");
    }
}
