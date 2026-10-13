using System.Reflection;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Architecture;

/// <summary>
/// #4168 (5g-a) — i percorsi che rispondono a una domanda eseguono le precondizioni nello stesso
/// punto: <c>IAnswerPreconditions</c>.
///
/// <para>
/// <b>Perché strutturale.</b> ADR-095 D3 mette l'accesso al passo 1 e la quota per piano al passo 2
/// di un'unica pipeline. Oggi i percorsi sono tre (la chat, la domanda singola, la ricerca fra
/// giochi) e ciascuno chiamava per conto suo il servizio d'accesso. Finché è così, ogni regola
/// nuova — la quota è la prossima — va aggiunta tre volte, e quella dimenticata non rompe nessun
/// test: il percorso che non la esegue risponde lo stesso.
/// </para>
/// <para>
/// <b>Cosa vede e cosa no.</b> Guarda da chi dipende un tipo: i parametri di tutti i suoi
/// costruttori e i suoi campi. Non vede se il passo viene poi chiamato: quello lo provano i test
/// dei percorsi, che con il cancello spento diventano rossi. E non vede un servizio risolto a mano
/// da <c>IServiceProvider</c>, che per questo ai percorsi è vietato.
/// </para>
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Architecture")]
[Trait("Issue", "4168")]
public sealed class AnswerPathsSharePreconditionsArchitectureTests
{
    private const string Preconditions =
        "Api.BoundedContexts.KnowledgeBase.Application.Services.Answering.IAnswerPreconditions";

    private const string AccessService =
        "Api.BoundedContexts.KnowledgeBase.Application.Services.IRagAccessService";

    private const string TheSharedStep = "è il passo condiviso";

    private const string AnswersOutsideTheSharedStep =
        "genera con l'LLM sulla KB e ha un cancello suo: se conta per la quota del piano si decide con la 5g-b";

    private const string NotAnAnswer =
        "non risponde a una domanda: cerca, gestisce la KB o legge lo stato dell'accesso";

    /// <summary>I percorsi che rispondono a una domanda.</summary>
    private static readonly string[] s_answerPaths =
    {
        "Api.BoundedContexts.KnowledgeBase.Application.Services.Answering.AnswerPipeline",
        "Api.BoundedContexts.KnowledgeBase.Application.Queries.AskQuestionQueryHandler",
        "Api.BoundedContexts.KnowledgeBase.Application.Services.Answering.CrossGameAnswerFlow",
    };

    /// <summary>
    /// Chi dipende dal servizio d'accesso, e perché non passa dal passo condiviso. Un tipo nuovo
    /// che lo riceve deve finire qui con una ragione, oppure fra i percorsi qui sopra.
    /// </summary>
    private static readonly Dictionary<string, string> s_usersOfTheAccessService = new(StringComparer.Ordinal)
    {
        ["Api.BoundedContexts.KnowledgeBase.Application.Services.Answering.AnswerPreconditions"] = TheSharedStep,

        ["Api.BoundedContexts.KnowledgeBase.Application.Commands.AskArbiterCommandHandler"] = AnswersOutsideTheSharedStep,
        ["Api.BoundedContexts.KnowledgeBase.Application.Commands.TutorQueryCommandHandler"] = AnswersOutsideTheSharedStep,
        ["Api.BoundedContexts.KnowledgeBase.Application.Commands.SuggestPlayerMoveCommandHandler"] = AnswersOutsideTheSharedStep,
        ["Api.BoundedContexts.KnowledgeBase.Application.Queries.StreamSetupGuideQueryHandler"] = AnswersOutsideTheSharedStep,

        ["Api.BoundedContexts.KnowledgeBase.Application.Queries.SearchQueryHandler"] = NotAnAnswer,
        ["Api.BoundedContexts.KnowledgeBase.Application.Queries.GlobalKbSearch.GlobalKbSearchQueryHandler"] = NotAnAnswer,
        ["Api.BoundedContexts.KnowledgeBase.Application.Queries.GetKnowledgeBaseStatusQueryHandler"] = NotAnAnswer,
        ["Api.BoundedContexts.KnowledgeBase.Application.Commands.CreateChatSessionCommandHandler"] = NotAnAnswer,
        ["Api.BoundedContexts.KnowledgeBase.Application.Commands.LinkExistingKbToGameCommandHandler"] = NotAnAnswer,
        ["Api.BoundedContexts.KnowledgeBase.Application.Commands.RebuildRaptorCommandHandler"] = NotAnAnswer,
        ["Api.BoundedContexts.KnowledgeBase.Application.Commands.ReindexGameKbCommandHandler"] = NotAnAnswer,
        ["Api.BoundedContexts.GameToolkit.Application.Commands.GenerateToolkitFromKbHandler"] = NotAnAnswer,
        ["Api.BoundedContexts.UserLibrary.Application.Commands.DeclareOwnershipCommandHandler"] = NotAnAnswer,
        ["Api.BoundedContexts.UserLibrary.Application.Queries.GetGameDetailQueryHandler"] = NotAnAnswer,
    };

    private const BindingFlags Instance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    /// <summary>I tipi da cui dipende: parametri di tutti i costruttori e campi d'istanza.</summary>
    private static HashSet<string> DependenciesOf(Type type) =>
        type.GetConstructors(Instance).SelectMany(c => c.GetParameters()).Select(p => p.ParameterType)
            .Concat(type.GetFields(Instance | BindingFlags.DeclaredOnly).Select(f => f.FieldType))
            .Select(t => t.FullName)
            .Where(name => name is not null)
            .Select(name => name!)
            .ToHashSet(StringComparer.Ordinal);

    private static Type TypeNamed(string typeName)
    {
        var type = typeof(Program).Assembly.GetType(typeName, throwOnError: false);
        type.Should().NotBeNull(
            $"'{typeName}' è nell'inventario di questo test: se è stato rinominato o tolto, " +
            "aggiorna l'inventario invece di lasciarlo divergere dal codice");
        return type!;
    }

    [Fact]
    public void EveryAnswerPath_RunsThePreconditionsInTheSharedStep()
    {
        foreach (var path in s_answerPaths)
        {
            DependenciesOf(TypeNamed(path)).Should().Contain(
                Preconditions,
                $"{path} deve eseguire accesso e quota tramite il passo condiviso");
        }
    }

    [Fact]
    public void NoAnswerPath_AsksTheAccessServiceOnItsOwn()
    {
        foreach (var path in s_answerPaths)
        {
            var dependencies = DependenciesOf(TypeNamed(path));

            dependencies.Should().NotContain(
                AccessService,
                $"{path} non deve controllare l'accesso per conto suo: una regola aggiunta al passo " +
                "condiviso (la quota, per esempio) non lo raggiungerebbe");
            dependencies.Should().NotContain(
                typeof(IServiceProvider).FullName,
                $"{path} non deve risolvere i servizi a mano: questo test guarda da chi dipende, " +
                "e un servizio preso da IServiceProvider non lo vedrebbe");
        }
    }

    /// <summary>
    /// L'inventario di chi riceve il servizio d'accesso. Serve alla 5g-b: quattro gestori generano
    /// con l'LLM sulla KB senza passare dal passo condiviso, e una quota messa lì non li
    /// raggiungerebbe. Qui stanno scritti, con la ragione, invece di restare un fatto da riscoprire.
    /// </summary>
    [Fact]
    public void EveryUserOfTheAccessService_IsClassified()
    {
        var actual = typeof(Program).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.FullName is not null)
            // Le classi che il compilatore genera per i metodi async e le chiusure tengono in un
            // campo ciò che il metodo usa: non sono chi dipende dal servizio, lo è il tipo che le contiene.
            .Where(t => !t.Name.Contains('<', StringComparison.Ordinal))
            .Where(t => DependenciesOf(t).Contains(AccessService))
            .Select(t => t.FullName!)
            .ToHashSet(StringComparer.Ordinal);
        var declared = s_usersOfTheAccessService.Keys.ToHashSet(StringComparer.Ordinal);

        actual.Except(declared).Should().BeEmpty(
            "un tipo che riceve IRagAccessService va classificato: se risponde a una domanda passa " +
            "da IAnswerPreconditions, altrimenti va aggiunto all'inventario con la sua ragione");
        declared.Except(actual).Should().BeEmpty(
            "l'inventario nomina tipi che non ricevono più il servizio d'accesso: va aggiornato");
        s_usersOfTheAccessService.Keys.Should().NotIntersectWith(
            s_answerPaths, "un percorso che risponde non può stare anche fra le eccezioni");
    }
}
