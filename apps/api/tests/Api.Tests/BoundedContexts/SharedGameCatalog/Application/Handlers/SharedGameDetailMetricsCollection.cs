using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

/// <summary>
/// Serializza le classi di test che emettono sul meter condiviso gli strumenti
/// <c>meepleai.shared_game_detail.*</c>.
/// </summary>
/// <remarks>
/// <para>
/// 🔴 Un <c>MeterListener</c> è globale al <c>Meter</c> di processo di <c>MeepleAiMetrics</c>: non
/// filtra per test, perché non ha niente su cui filtrare — i tag di questi strumenti sono
/// <c>source</c> e <c>cache_outcome</c>, nessuno dei quali identifica il chiamante. Con il default
/// di <c>xunit.runner.json</c> (<c>parallelizeTestCollections: true</c>) due classi in collection
/// diverse che esercitano <c>GetSharedGameByIdQueryHandler</c> si catturano le misure a vicenda, e
/// un'asserzione <c>HaveCount(2)</c> ne vede tre.
/// </para>
/// <para>
/// Osservato il 2026-10-04 in un run locale di <c>Category=Unit</c> (22.683 test in un processo):
/// <c>Handle_RepeatedCall_SecondCallEmitsSourceHit</c> falliva con
/// <c>Expected cacheHitSources to contain 2 item(s), but found 3: {"origin", "hit", "origin"}</c> —
/// quel terzo <c>"origin"</c> è la misura di un'altra classe. In isolamento passava, che è la firma
/// di questo difetto e il motivo per cui non va cercato nel test che lo riporta.
/// </para>
/// <para>
/// È lo stesso rimedio già adottato nel repo per <c>CoverResolutionMetricsCollection</c>,
/// <c>AgentGroundingMetricsCollection</c> e <c>GamebookMeterCollection</c>. Come lì, 🔴 <b>una
/// classe che EMETTE senza asserire va comunque qui dentro</b>: altrimenti continua a inquinare chi
/// asserisce. Perciò la collection include anche <c>GetSharedGameByIdQueryHandlerTests</c> e
/// <c>GetSharedGameByIdQueryHandlerCoverFocalTests</c>, che invocano il handler senza guardarne le
/// metriche.
/// </para>
/// <para>
/// <b>Limite dichiarato.</b> <c>GetSharedGameByIdQueryHandlerCrossBcTests</c> emette gli stessi
/// strumenti ma vive in <c>Integration-GroupC</c>, dove la sua fixture di database la tiene: non può
/// entrare qui. Nel gate veloce la cosa non si manifesta, perché quel gate esclude
/// <c>Category=Integration</c>; in un <c>dotnet test</c> senza filtri l'inquinamento resta
/// possibile. Chiuderlo richiede un tag che identifichi il chiamante, cioè una modifica di
/// produzione per comodità di un test: non vale, ma va saputo invece che scoperto.
/// </para>
/// </remarks>
[CollectionDefinition("SharedGameDetailMetrics", DisableParallelization = true)]
public sealed class SharedGameDetailMetricsCollection
{
}
