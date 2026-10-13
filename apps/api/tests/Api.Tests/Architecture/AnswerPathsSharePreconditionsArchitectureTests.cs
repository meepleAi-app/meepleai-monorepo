using System.Reflection;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Architecture;

/// <summary>
/// #4168 (5g-a) — i tre percorsi che rispondono a una domanda eseguono le precondizioni nello
/// stesso punto: <c>IAnswerPreconditions</c>.
///
/// <para>
/// <b>Perché strutturale.</b> ADR-095 D3 mette l'accesso al passo 1 e la quota per piano al passo 2
/// di un'unica pipeline. Oggi i percorsi sono tre (la chat, la domanda singola, la ricerca fra
/// giochi) e ciascuno chiamava per conto suo il servizio d'accesso. Finché è così, ogni regola
/// nuova — la quota è la prossima — va aggiunta tre volte, e quella dimenticata non rompe nessun
/// test: il percorso che non la esegue risponde lo stesso.
/// </para>
/// <para>
/// Il test difende la proprietà che lo impedisce: nessuno dei tre dipende direttamente da
/// <c>IRagAccessService</c>, tutti e tre da <c>IAnswerPreconditions</c>. Un quarto percorso che
/// risponde va aggiunto all'inventario.
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

    /// <summary>I percorsi che rispondono a una domanda.</summary>
    private static readonly string[] s_answerPaths =
    {
        "Api.BoundedContexts.KnowledgeBase.Application.Services.Answering.AnswerPipeline",
        "Api.BoundedContexts.KnowledgeBase.Application.Queries.AskQuestionQueryHandler",
        "Api.BoundedContexts.KnowledgeBase.Application.Services.Answering.CrossGameAnswerFlow",
    };

    private static ParameterInfo[] ConstructorParametersOf(string typeName)
    {
        var type = typeof(Program).Assembly.GetType(typeName, throwOnError: false);
        type.Should().NotBeNull(
            $"'{typeName}' è nell'inventario di questo test: se è stato rinominato o tolto, " +
            "aggiorna l'inventario invece di lasciarlo divergere dal codice");

        return type!
            .GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .OrderByDescending(c => c.GetParameters().Length)
            .First()
            .GetParameters();
    }

    [Fact]
    public void EveryAnswerPath_RunsThePreconditionsInTheSharedStep()
    {
        foreach (var path in s_answerPaths)
        {
            ConstructorParametersOf(path).Select(p => p.ParameterType.FullName).Should().Contain(
                Preconditions,
                $"{path} deve eseguire accesso e quota tramite il passo condiviso");
        }
    }

    [Fact]
    public void NoAnswerPath_AsksTheAccessServiceOnItsOwn()
    {
        foreach (var path in s_answerPaths)
        {
            ConstructorParametersOf(path).Select(p => p.ParameterType.FullName).Should().NotContain(
                AccessService,
                $"{path} non deve controllare l'accesso per conto suo: una regola aggiunta al passo " +
                "condiviso (la quota, per esempio) non lo raggiungerebbe");
        }
    }
}
