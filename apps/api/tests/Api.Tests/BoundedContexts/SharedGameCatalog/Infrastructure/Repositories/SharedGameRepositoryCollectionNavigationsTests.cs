using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;

/// <summary>
/// #4596 — ogni collezione figlia di <c>SharedGameEntity</c> ha un proprietario dichiarato.
/// </summary>
/// <remarks>
/// <para>
/// Le quick question esistevano come navigazione dell'entità e come collezione dell'aggregato, ma
/// <c>SharedGameRepository</c> non le nominava mai: nessun test lo vedeva perché nessuno guardava la
/// collezione. Stesso difetto di errata (#4543) e FAQ (#4557). Il test prende le navigazioni dal
/// modello EF: una collezione aggiunta domani all'entità, senza una riga qui, lo fa fallire con il suo
/// nome, e chi la aggiunge deve decidere chi la scrive.
/// </para>
/// <para>
/// Cosa NON prova: che una collezione dichiarata «riconciliata da Update» lo sia davvero. Lo provano i
/// test di ciascuna (<c>SharedGameRepositoryErrataTests</c>, <c>SharedGameRepositoryFaqTests</c>,
/// <c>SharedGameRepositoryQuickQuestionTests</c> e i loro gemelli su Postgres). Questo test fa solo
/// in modo che la domanda venga posta.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "4596")]
public sealed class SharedGameRepositoryCollectionNavigationsTests : IDisposable
{
    /// <summary>
    /// Chi scrive ogni collezione figlia. Una collezione nuova va aggiunta qui con la ragione.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, (Owner Owner, string Reason)> Collections =
        new Dictionary<string, (Owner, string)>(StringComparer.Ordinal)
        {
            ["Faqs"] = (Owner.ReconciledByUpdate, "SharedGameRepository.Update → ReconcileChildren (#4557)"),
            ["Erratas"] = (Owner.ReconciledByUpdate, "SharedGameRepository.Update → ReconcileChildren (#4543)"),
            ["QuickQuestions"] = (Owner.ReconciledByUpdate, "SharedGameRepository.Update → ReconcileChildren (#4596)"),
            ["CoverAssignments"] = (Owner.ReconciledByDedicatedCall, "ReconcileCoverAssignmentsAsync, chiamata a parte dai comandi sulle cover (#3470)"),
            ["Designers"] = (Owner.EditedOnTheEntity, "get-or-create per nome in AddAsync (#3153); Update non li tocca"),
            ["Publishers"] = (Owner.EditedOnTheEntity, "get-or-create per nome in AddAsync (#3153); Update non li tocca"),
            ["Categories"] = (Owner.EditedOnTheEntity, "scritte sull'entità da UpdateSharedGameCommandHandler e dall'import BGG"),
            ["Mechanics"] = (Owner.EditedOnTheEntity, "scritte sull'entità da UpdateSharedGameCommandHandler e dall'import BGG"),
            ["Documents"] = (Owner.OwnRepository, "ISharedGameDocumentRepository; l'aggregato non porta i documenti"),
            ["Contributors"] = (Owner.OwnRepository, "ContributorRepository"),
        };

    private enum Owner
    {
        /// <summary>Update() la riconcilia: INSERT delle nuove, UPDATE delle colonne cambiate, DELETE delle lette e tolte.</summary>
        ReconciledByUpdate,
        ReconciledByDedicatedCall,
        EditedOnTheEntity,
        OwnRepository,
    }

    private readonly MeepleAiDbContext _db = TestDbContextFactory.CreateInMemoryDbContext();

    public void Dispose() => _db.Dispose();

    private IReadOnlyList<string> CollectionNavigationsOfTheRow()
    {
        var entityType = _db.Model.FindEntityType(typeof(SharedGameEntity))!;
        return entityType.GetNavigations().Where(n => n.IsCollection).Select(n => n.Name)
            .Concat(entityType.GetSkipNavigations().Select(n => n.Name))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    [Fact]
    public void EveryCollectionNavigationOfSharedGameEntity_HasADeclaredOwner()
    {
        var navigations = CollectionNavigationsOfTheRow();
        navigations.Should().NotBeEmpty("le navigazioni vengono dal modello EF, che deve avere SharedGameEntity");

        navigations.Except(Collections.Keys, StringComparer.Ordinal).Should().BeEmpty(
            "una collezione figlia nuova va dichiarata in Collections con chi la scrive. Se è dell'aggregato, " +
            "SharedGameRepository deve leggerla in MapToDomain e riconciliarla in Update (vedi QuickQuestions, #4596)");
    }

    [Fact]
    public void EveryDeclaredCollection_StillExistsInTheModel()
    {
        Collections.Keys.Except(CollectionNavigationsOfTheRow(), StringComparer.Ordinal).Should().BeEmpty(
            "una voce di Collections senza navigazione corrispondente è rimasta indietro: toglila");
    }

    [Fact]
    public void EveryCollectionReconciledByUpdate_IsACollectionOfTheAggregate()
    {
        // Se Update riconcilia una collezione, l'aggregato deve portarla: altrimenti non c'è niente da
        // riconciliare e la voce dichiara una cosa che non succede.
        var missing = Collections
            .Where(c => c.Value.Owner == Owner.ReconciledByUpdate)
            .Select(c => c.Key)
            .Where(name => typeof(SharedGame).GetProperty(name) is null)
            .ToList();

        missing.Should().BeEmpty();
    }
}
