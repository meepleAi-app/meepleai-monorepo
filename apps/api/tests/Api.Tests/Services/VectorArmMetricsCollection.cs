using Xunit;

namespace Api.Tests.Services;

/// <summary>
/// Serializza le classi che esercitano <c>HybridSearchService.SearchAsync</c>, perché tutte
/// emettono il contatore condiviso <c>meepleai.rag.vector_arm.outcomes</c> (#3786).
/// </summary>
/// <remarks>
/// <para>
/// Un <c>MeterListener</c> è globale al <c>Meter</c> di processo di <c>MeepleAiMetrics</c>: due
/// classi che girano in collection parallele — il default di <c>xunit.runner.json</c> è
/// <c>parallelizeTestCollections: true</c> — catturano le misure l'una dell'altra, e
/// un'asserzione su un conteggio esatto vede saltuariamente le misurazioni della vicina.
/// Osservato: <c>ASearchWithResultsIsRecordedAsHit</c> passava da sola e falliva nella suite.
/// </para>
/// <para>
/// Stesso schema già adottato in <c>CoverResolutionMetricsCollection</c> e
/// <c>AgentGroundingMetricsCollection</c>, inclusa la parte che si dimentica facilmente: vanno
/// nella collection anche le classi che <b>emettono</b> la metrica senza asserirci sopra
/// — altrimenti restano libere di inquinare chi lo fa.
/// </para>
/// <para>
/// #4168 (5e-c-2): la emette anche <c>SearchQueryHandler</c>, cioè il recupero per gioco di ogni
/// risposta, e le classi che lo esercitano sono troppe per stare qui. Le protegge la stessa
/// <c>DisableParallelization</c>: i test di una collection così non girano in parallelo con nessun
/// altro test (documentazione di xUnit, «Running tests in parallel»), quindi nessun'altra classe
/// emette mentre questa misura. Le classi che ASSERISCONO sul
/// contatore restano comunque qui dentro.
/// </para>
/// </remarks>
[CollectionDefinition("VectorArmMetrics", DisableParallelization = true)]
public sealed class VectorArmMetricsCollection
{
}
